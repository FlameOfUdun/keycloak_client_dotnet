using System.Text.Json;

namespace Winche.KeycloakClient.Interfaces;

public interface IKeycloakWebhookService
{
    Task HandleEventAsync(JsonElement payload, CancellationToken ct);
    Task RegisterWebhooksAsync(CancellationToken ct);
}
