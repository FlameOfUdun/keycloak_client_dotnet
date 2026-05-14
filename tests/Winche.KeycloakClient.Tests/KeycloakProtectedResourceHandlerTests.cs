using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;

namespace Winche.KeycloakClient.Tests;

public class KeycloakProtectedResourceHandlerTests
{
    private sealed class StubAuthz(Func<string, string, bool> decide) : IKeycloakAuthorizationService
    {
        public Task<bool> AuthorizeAsync(string resource, string scope, CancellationToken ct = default) =>
            Task.FromResult(decide(resource, scope));

        public Task RequireAsync(string resource, string scope, CancellationToken ct = default) =>
            decide(resource, scope)
                ? Task.CompletedTask
                : throw new KeycloakAuthorizationException($"denied {resource}#{scope}");
    }

    private sealed class ThrowingAuthz : IKeycloakAuthorizationService
    {
        public Task<bool> AuthorizeAsync(string resource, string scope, CancellationToken ct = default) =>
            throw new KeycloakAuthorizationException("no bearer token");

        public Task RequireAsync(string resource, string scope, CancellationToken ct = default) =>
            throw new KeycloakAuthorizationException("no bearer token");
    }

    private static AuthorizationHandlerContext BuildContext(KeycloakProtectedResourceRequirement requirement)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "Bearer"));
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }

    [Fact]
    public async Task RequirementSucceeded_WhenAuthzGrants()
    {
        var handler = new KeycloakProtectedResourceHandler(new StubAuthz((r, s) => true));
        var requirement = new KeycloakProtectedResourceRequirement("document", "read");
        var ctx = BuildContext(requirement);

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task RequirementNotSucceeded_WhenAuthzDenies()
    {
        var handler = new KeycloakProtectedResourceHandler(new StubAuthz((r, s) => false));
        var requirement = new KeycloakProtectedResourceRequirement("document", "read");
        var ctx = BuildContext(requirement);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task RequirementNotSucceeded_WhenAuthzThrows()
    {
        // KeycloakAuthorizationException from the service (e.g., missing bearer) must be swallowed
        // by the handler so ASP.NET returns 403, not 500.
        var handler = new KeycloakProtectedResourceHandler(new ThrowingAuthz());
        var requirement = new KeycloakProtectedResourceRequirement("document", "read");
        var ctx = BuildContext(requirement);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }
}
