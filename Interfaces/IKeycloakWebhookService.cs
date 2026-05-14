using System.Text.Json;

namespace KeycloakClient.Interfaces;

public interface IKeycloakWebhookService
{
    Task HandleEventAsync(JsonElement payload, CancellationToken ct);
    Task RegisterWebhooksAsync(CancellationToken ct);
}
