using Microsoft.Extensions.Configuration;

namespace Winche.KeycloakClient.Models;

public sealed record KeycloakClientOptions
{
    public const string SectionName = "Keycloak";

    [ConfigurationKeyName("Server")]
    public string Server { get; set; } = string.Empty;

    [ConfigurationKeyName("Realm")]
    public string Realm { get; set; } = string.Empty;

    [ConfigurationKeyName("Resource")]
    public string Resource { get; set; } = string.Empty;

    [ConfigurationKeyName("Credentials")]
    public KeycloakCredentialsOptions? Credentials { get; set; }

    [ConfigurationKeyName("Agent")]
    public string? Agent { get; set; }

    [ConfigurationKeyName("Webhook")]
    public KeycloakWebhookOptions? Webhook { get; set; }

    [ConfigurationKeyName("Authentication")]
    public KeycloakAuthenticationOptions? Authentication { get; set; }

    [ConfigurationKeyName("Authorization")]
    public KeycloakAuthorizationOptions? Authorization { get; set; }
}

public sealed record KeycloakCredentialsOptions
{
    [ConfigurationKeyName("Secret")]
    public string? Secret { get; set; }
}

public sealed record KeycloakWebhookOptions
{
    [ConfigurationKeyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [ConfigurationKeyName("URL")]
    public string Url { get; set; } = string.Empty;

    [ConfigurationKeyName("Secret")]
    public string? Secret { get; set; }
}