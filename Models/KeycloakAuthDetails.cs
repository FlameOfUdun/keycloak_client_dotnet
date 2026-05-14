using System.Text.Json.Serialization;

namespace KeycloakClient.Models;

public sealed record KeycloakAuthDetails
{
    [JsonPropertyName("realmId")]
    public string RealmId { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
