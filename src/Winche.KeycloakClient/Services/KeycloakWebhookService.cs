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
    private readonly IKeycloakClientService _client = client;
    private readonly IOptions<KeycloakClientOptions> _options = options;
    private readonly ILogger<KeycloakWebhookService> _logger = logger;
    private readonly IEnumerable<KeycloakEventHandler> _handlers = handlers;

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
            _logger.LogError(ex, "[Keycloak] Failed to parse incoming event.");
            return;
        }

        var targets = _handlers.Where(h => h.Types.Contains(parsed.Type)).ToArray();
        if (targets.Length == 0)
        {
            _logger.LogWarning("[Keycloak] No handler registered for event type '{EventType}'.", parsed.Type);
            return;
        }

        var failures = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = targets.Select(async h =>
        {
            try
            {
                await h.HandleAsync(parsed, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Keycloak] Handler '{Handler}' failed for event '{EventType}'.", h.GetType().Name, parsed.Type);
                failures.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Surfaced so the endpoint answers 500 and Keycloak retries, instead of the event being lost.
        if (!failures.IsEmpty)
        {
            throw new KeycloakWebhookHandlerException(parsed.Type, [.. failures]);
        }
    }

    public async Task RegisterWebhookAsync(CancellationToken ct = default)
    {
        var webhookDef = _options.Value.Webhook;
        if (webhookDef is null || string.IsNullOrWhiteSpace(webhookDef.Url))
        {
            _logger.LogInformation("[Keycloak] No webhook configured. Registration skipped.");
            return;
        }

        var existing = (await _client.GetWebhooksAsync(ct))
            .FirstOrDefault(h => string.Equals(h.Url, webhookDef.Url, StringComparison.OrdinalIgnoreCase));

        var eventTypes = _handlers.SelectMany(h => h.Types).Distinct().ToArray();

        if (existing is not null
            && webhookDef.Enabled
            && !webhookDef.Recreate
            && eventTypes.Length > 0
            && SubscribesTo(existing, eventTypes))
        {
            _logger.LogInformation(
                "[Keycloak] Webhook '{WebhookId}' for URL '{WebhookUrl}' already matches configuration; leaving it in place.",
                existing.Id,
                webhookDef.Url);
            return;
        }

        if (existing?.Id is { } existingId)
        {
            _logger.LogInformation("[Keycloak] Removing existing webhook '{WebhookId}' for URL '{WebhookUrl}'.", existingId, webhookDef.Url);
            await _client.DeleteWebhookAsync(existingId, ct);
        }

        if (!webhookDef.Enabled)
        {
            _logger.LogInformation("[Keycloak] Webhook disabled in configuration. Skipping creation.");
            return;
        }

        if (eventTypes.Length == 0)
        {
            _logger.LogInformation("[Keycloak] No event handlers registered; no event types to subscribe. Registration skipped.");
            return;
        }

        var webhook = new KeycloakWebhook
        {
            Enabled = true,
            Url = webhookDef.Url,
            Secret = webhookDef.Secret,
            EventTypes = eventTypes
        };

        await _client.CreateWebhookAsync(webhook, ct);

        _logger.LogInformation(
            "[Keycloak] Webhook registered for URL '{WebhookUrl}' with {EventTypeCount} event type(s).",
            webhook.Url,
            eventTypes.Length);
    }

    private static bool SubscribesTo(KeycloakWebhook existing, string[] eventTypes)
    {
        if (!existing.Enabled)
        {
            return false;
        }

        var current = existing.EventTypes ?? [];

        return current.Length == eventTypes.Length
            && new HashSet<string>(current, StringComparer.Ordinal).SetEquals(eventTypes);
    }
}