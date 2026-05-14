using System.Text.Json.Serialization;

namespace Winche.KeycloakClient.Models;

public sealed record KeycloakGroup
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("subGroups")]
    public List<KeycloakGroup>? SubGroups { get; set; }
}
