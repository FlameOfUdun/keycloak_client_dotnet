using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Winche.KeycloakClient.Services;

public sealed class KeycloakProtectedResourceHandler(
    ILogger<KeycloakProtectedResourceHandler> logger,
    IKeycloakAuthorizationService authz
) : AuthorizationHandler<KeycloakProtectedResourceRequirement>
{
    private readonly ILogger<KeycloakProtectedResourceHandler> _logger = logger;
    private readonly IKeycloakAuthorizationService _authz = authz;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, KeycloakProtectedResourceRequirement requirement)
    {
        try
        {
            if (await _authz.AuthorizeAsync(requirement.Resource, requirement.Scope, CancellationToken.None))
            {
                context.Succeed(requirement);
            }
        }
        catch (KeycloakAuthorizationException ex)
        {
            _logger.LogError(ex, "Authorization failed for resource {Resource} and scope {Scope}", requirement.Resource, requirement.Scope);
        }
    }
}
