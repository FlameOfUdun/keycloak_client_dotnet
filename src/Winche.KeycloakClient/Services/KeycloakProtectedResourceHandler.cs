using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Authorization;

namespace Winche.KeycloakClient.Services;

public sealed class KeycloakProtectedResourceHandler(IKeycloakAuthorizationService authz)
    : AuthorizationHandler<KeycloakProtectedResourceRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        KeycloakProtectedResourceRequirement requirement)
    {
        try
        {
            if (await authz.AuthorizeAsync(requirement.Resource, requirement.Scope, CancellationToken.None))
            {
                context.Succeed(requirement);
            }
        }
        catch (KeycloakAuthorizationException)
        {
        }
    }
}
