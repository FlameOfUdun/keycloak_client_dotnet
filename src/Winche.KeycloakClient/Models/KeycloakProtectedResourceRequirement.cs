using Microsoft.AspNetCore.Authorization;

namespace Winche.KeycloakClient.Models;

public sealed class KeycloakProtectedResourceRequirement(string resource, string scope) : IAuthorizationRequirement
{
    public string Resource { get; } = resource;
    public string Scope { get; } = scope;
}
