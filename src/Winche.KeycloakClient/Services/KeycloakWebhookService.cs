using Winche.KeycloakClient.Abstraction;
using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Winche.KeycloakClient.Services;

public sealed class KeycloakWebhookService(
    IKeycloakClientService client,
    IOptions<KeycloakClientOptions> options,
    ILogger<KeycloakWebhookService> logger,
    IEnumerable<KeycloakEventHandler> handlers
) : IKeycloakWebhookService
{
    public async Task HandleEventAsync(JsonElement payload, CancellationToken ct = default)
    {
        KeycloakWebhookEvent parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<KeycloakWebhookEvent>(payload)
                ?? throw new JsonException("Deserialized Keycloak event was null.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Keycloak] Failed to parse incoming event.");
            return;
        }

        var targets = handlers.Where(h => h.Types.Contains(parsed.Type)).ToArray();
        if (targets.Length == 0)
        {
            logger.LogWarning("[Keycloak] No handler registered for event type '{EventType}'.", parsed.Type);
            return;
        }

        var tasks = targets.Select(async h =>
        {
            try
            {
                await h.HandleAsync(parsed, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Keycloak] Handler '{Handler}' failed for event '{EventType}'.", h.GetType().Name, parsed.Type);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task RegisterWebhooksAsync(CancellationToken ct = default)
    {
        var webhookDef = options.Value.Webhook;
        if (webhookDef is null || string.IsNullOrWhiteSpace(webhookDef.Url))
        {
            logger.LogInformation("[Keycloak] No webhook configured. Registration skipped.");
            return;
        }

        var existing = (await client.GetWebhooksAsync(ct))
            .FirstOrDefault(h => string.Equals(h.Url, webhookDef.Url, StringComparison.OrdinalIgnoreCase));

        if (existing?.Id is { } existingId)
        {
            logger.LogInformation("[Keycloak] Removing existing webhook '{WebhookId}' for URL '{WebhookUrl}'.", existingId, webhookDef.Url);
            await client.DeleteWebhookAsync(existingId, ct);
        }

        if (!webhookDef.Enabled)
        {
            logger.LogInformation("[Keycloak] Webhook disabled in configuration. Skipping creation.");
            return;
        }

        var eventTypes = handlers.SelectMany(h => h.Types).Distinct().ToArray();
        if (eventTypes.Length == 0)
        {
            logger.LogInformation("[Keycloak] No event handlers registered; no event types to subscribe. Registration skipped.");
            return;
        }

        var webhook = new KeycloakWebhook
        {
            Enabled = true,
            Url = webhookDef.Url,
            Secret = webhookDef.Secret,
            EventTypes = eventTypes
        };

        await client.CreateWebhookAsync(webhook, ct);
        logger.LogInformation("[Keycloak] Webhook registered for URL '{WebhookUrl}' with {EventTypeCount} event type(s).", webhook.Url, eventTypes.Length);
    }
}