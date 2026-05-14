using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Text.Json;

namespace Winche.KeycloakClient.Services;

internal static class KeycloakClaimsTransformer
{
    public static void Transform(TokenValidatedContext context, KeycloakClientOptions options)
    {
        if (context.Principal is null) return;
        var failure = TransformCore(context.Principal, options);
        if (failure is not null) context.Fail(failure);
    }

    /// <summary>
    /// Pure-logic core: mutates the principal's <see cref="ClaimsIdentity"/> in place and returns
    /// a non-null failure message when the token should be rejected. Today this only ever returns
    /// <c>null</c>; "is this token for me?" is enforced by the framework's audience validation
    /// (see <see cref="KeycloakJwtBearerConfigurator"/>), and roles are flattened into role claims.
    /// Separated from <see cref="Transform"/> so it can be unit-tested without constructing a
    /// <see cref="TokenValidatedContext"/>.
    /// </summary>
    internal static string? TransformCore(ClaimsPrincipal principal, KeycloakClientOptions options)
    {
        if (principal.Identity is not ClaimsIdentity identity) return null;

        var auth = options.Authentication ?? new KeycloakAuthenticationOptions();

        if (auth.RolesSource is KeycloakRolesSource.Realm or KeycloakRolesSource.RealmAndResource)
        {
            var realmAccess = principal.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccess))
            {
                AddRolesFromJson(identity, realmAccess, auth.RealmRolePrefix);
            }
        }

        if (auth.RolesSource is KeycloakRolesSource.Resource or KeycloakRolesSource.RealmAndResource)
        {
            var resourceAccessRaw = principal.FindFirst("resource_access")?.Value;
            if (!string.IsNullOrEmpty(resourceAccessRaw))
            {
                using var doc = JsonDocument.Parse(resourceAccessRaw);
                var clients = new List<string>(1 + auth.AdditionalResourceClients.Length) { options.Resource };
                clients.AddRange(auth.AdditionalResourceClients);
                foreach (var client in clients)
                {
                    if (doc.RootElement.TryGetProperty(client, out var clientObj) &&
                        clientObj.TryGetProperty("roles", out var rolesArr) &&
                        rolesArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var roleEl in rolesArr.EnumerateArray())
                        {
                            if (roleEl.GetString() is { } role)
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, (auth.ResourceRolePrefix ?? string.Empty) + role));
                            }
                        }
                    }
                }
            }
        }

        AddIfMissing(identity, ClaimTypes.Email, principal.FindFirst("email")?.Value);
        AddIfMissing(identity, ClaimTypes.GivenName, principal.FindFirst("given_name")?.Value);
        AddIfMissing(identity, ClaimTypes.Surname, principal.FindFirst("family_name")?.Value);
        return null;
    }

    private static void AddRolesFromJson(ClaimsIdentity identity, string json, string? prefix)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("roles", out var rolesArr) &&
            rolesArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var roleEl in rolesArr.EnumerateArray())
            {
                if (roleEl.GetString() is { } role)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, (prefix ?? string.Empty) + role));
                }
            }
        }
    }

    private static void AddIfMissing(ClaimsIdentity identity, string type, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (identity.HasClaim(c => c.Type == type)) return;
        identity.AddClaim(new Claim(type, value));
    }
}
