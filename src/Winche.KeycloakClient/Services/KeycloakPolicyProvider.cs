using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Winche.KeycloakClient.Services;

public sealed class KeycloakPolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefix = "kc:protected:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public KeycloakPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var remainder = policyName[Prefix.Length..];
            var sep = remainder.IndexOf(':');
            if (sep > 0 && sep < remainder.Length - 1)
            {
                var resource = remainder[..sep];
                var scope = remainder[(sep + 1)..];

                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new KeycloakProtectedResourceRequirement(resource, scope))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}
