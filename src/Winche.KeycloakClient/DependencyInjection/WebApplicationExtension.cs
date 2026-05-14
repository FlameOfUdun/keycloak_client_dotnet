using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Winche.KeycloakClient.DependencyInjection;

public static class WebApplicationExtensions
{
    public static WebApplication UseKeycloakWebHooks(this WebApplication app, string path = "/webhooks/keycloak")
    {
        app.MapPost(path, async (HttpRequest request, IKeycloakWebhookService service, IOptions<KeycloakClientOptions> options, CancellationToken ct) =>
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, ct);
            var body = ms.ToArray();

            var secret = options.Value.Webhook?.Secret;
            if (!string.IsNullOrEmpty(secret))
            {
                var signatureHeader = request.Headers["X-Keycloak-Signature"].FirstOrDefault();
                if (string.IsNullOrEmpty(signatureHeader))
                {
                    return Results.Unauthorized();
                }

                byte[] expected;
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
                {
                    expected = hmac.ComputeHash(body);
                }

                byte[] provided;
                try
                {
                    provided = Convert.FromHexString(signatureHeader);
                }
                catch (FormatException)
                {
                    return Results.Unauthorized();
                }

                if (provided.Length != expected.Length ||
                    !CryptographicOperations.FixedTimeEquals(provided, expected))
                {
                    return Results.Unauthorized();
                }
            }

            var payload = JsonSerializer.Deserialize<JsonElement>(body);
            await service.HandleEventAsync(payload, ct);
            return Results.Ok();
        }).AllowAnonymous();

        return app;
    }
}
