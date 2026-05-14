using System.Text.Json.Serialization;

namespace Winche.KeycloakClient.Models;

public sealed record KeycloakUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("emailVerified")]
    public bool? EmailVerified { get; set; }

    [JsonPropertyName("totp")]
    public bool? Totp { get; set; }

    [JsonPropertyName("requiredActions")]
    public List<string>? RequiredActions { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, List<string>>? Attributes { get; set; }
}
