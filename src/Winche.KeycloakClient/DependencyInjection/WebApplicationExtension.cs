using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Winche.KeycloakClient.DependencyInjection;

public static class WebApplicationExtensions
{
    public static WebApplication UseKeycloakWebHooks(this WebApplication app, string path = "/webhooks/keycloak")
    {
        app.MapPost(path, async (HttpRequest request, [FromBody] JsonElement payload, IKeycloakWebhookService service, IOptions<KeycloakClientOptions> options, CancellationToken ct) =>
        {
            var expectedSecret = options.Value.Webhook?.Secret;
            if (!string.IsNullOrEmpty(expectedSecret))
            {
                var providedSecret = request.Headers["X-Webhook-Secret"].FirstOrDefault();
                var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
                var providedBytes = Encoding.UTF8.GetBytes(providedSecret ?? "");
                if (providedBytes.Length != expectedBytes.Length || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
                    return Results.Unauthorized();
            }

            await service.HandleEventAsync(payload, ct);
            return Results.Ok();
        }).AllowAnonymous();

        return app;
    }
}
