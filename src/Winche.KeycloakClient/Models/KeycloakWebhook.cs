using System.Text.Json.Serialization;

namespace Winche.KeycloakClient.Models;

public sealed record KeycloakWebhook
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    [JsonPropertyName("eventTypes")]
    public string[]? EventTypes { get; set; }
}
