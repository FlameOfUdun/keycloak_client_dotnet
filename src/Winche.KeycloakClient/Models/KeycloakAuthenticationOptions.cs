using Microsoft.Extensions.Configuration;

namespace Winche.KeycloakClient.Models;

public enum KeycloakRolesSource
{
    Realm,
    Resource,
    RealmAndResource
}

public sealed record KeycloakAuthenticationOptions
{
    [ConfigurationKeyName("ValidateAudience")]
    public bool ValidateAudience { get; set; } = false;

    [ConfigurationKeyName("RolesSource")]
    public KeycloakRolesSource RolesSource { get; set; } = KeycloakRolesSource.RealmAndResource;

    [ConfigurationKeyName("RealmRolePrefix")]
    public string? RealmRolePrefix { get; set; }

    [ConfigurationKeyName("ResourceRolePrefix")]
    public string? ResourceRolePrefix { get; set; }

    [ConfigurationKeyName("AdditionalResourceClients")]
    public string[] AdditionalResourceClients { get; set; } = [];
}
