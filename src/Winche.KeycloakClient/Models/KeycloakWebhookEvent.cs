namespace Winche.KeycloakClient.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record KeycloakWebhookEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("realmId")]
    public string RealmId { get; set; } = string.Empty;

    [JsonPropertyName("realmName")]
    public string RealmName { get; set; } = string.Empty;

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = string.Empty;

    [JsonPropertyName("resourcePath")]
    public string ResourcePath { get; set; } = string.Empty;

    [JsonPropertyName("representation")]
    public string? RepresentationRaw { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;

    [JsonPropertyName("authDetails")]
    public KeycloakAuthDetails AuthDetails { get; set; } = new();

    [JsonPropertyName("details")]
    public Dictionary<string, string>? Details { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(Time);

    [JsonIgnore]
    public KeycloakUser? Representation => string.IsNullOrEmpty(RepresentationRaw)
        ? null
        : JsonSerializer.Deserialize<KeycloakUser>(RepresentationRaw);
}
