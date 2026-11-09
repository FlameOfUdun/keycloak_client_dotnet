using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Winche.KeycloakClient.DependencyInjection;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps the Keycloak webhook endpoint. Every event must carry a valid <c>X-Keycloak-Signature</c>
    /// (HMAC-SHA256 of the body with <c>Keycloak:Webhook:Secret</c>); without a configured secret the endpoint
    /// rejects everything (401) rather than accepting unsigned events. A handler failure answers 500 so
    /// Keycloak retries the event; handlers must therefore be idempotent.
    /// </summary>
    public static WebApplication UseKeycloakWebHooks(this WebApplication app, string path = "/webhooks/keycloak")
    {
        var configured = app.Services.GetRequiredService<IOptions<KeycloakClientOptions>>().Value.Webhook?.Secret;
        if (string.IsNullOrEmpty(configured))
        {
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Winche.KeycloakClient.Webhooks").LogError(
                "[Keycloak] Keycloak:Webhook:Secret is not configured: {Path} rejects every event until it is.", path);
        }

        app.MapPost(path, async (HttpRequest request, IKeycloakWebhookService service, IOptions<KeycloakClientOptions> options, CancellationToken ct) =>
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, ct);
            var body = ms.ToArray();

            var signature = request.Headers["X-Keycloak-Signature"].FirstOrDefault();
            if (!KeycloakWebhookSignature.IsValid(options.Value.Webhook?.Secret, body, signature))
            {
                return Results.Unauthorized();
            }

            JsonElement payload;
            try
            {
                payload = JsonSerializer.Deserialize<JsonElement>(body);
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            try
            {
                await service.HandleEventAsync(payload, ct);
            }
            catch (KeycloakWebhookHandlerException)
            {
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Results.Ok();
        }).AllowAnonymous();

        return app;
    }
}

/// <summary>Verifies Keycloak webhook signatures: hex HMAC-SHA256 of the raw body, compared in constant time.</summary>
public static class KeycloakWebhookSignature
{
    /// <summary>False when no secret is configured, the header is missing or malformed, or the signature doesn't match.</summary>
    public static bool IsValid(string? secret, byte[] body, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signatureHeader);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected);
    }
}
