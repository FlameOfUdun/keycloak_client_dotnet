using System.Text.Json.Serialization;

namespace KeycloakClient.Models;

public sealed record KeycloakCredential
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "password";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("temporary")]
    public bool Temporary { get; set; }
}
