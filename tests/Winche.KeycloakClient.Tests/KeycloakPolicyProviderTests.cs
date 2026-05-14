using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;

namespace Winche.KeycloakClient.Tests;

public class KeycloakPolicyProviderTests
{
    private static KeycloakPolicyProvider Build() =>
        new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task ValidProtectedResourceName_BuildsRequirementPolicy()
    {
        var provider = Build();

        var policy = await provider.GetPolicyAsync("kc:protected:document:read");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<KeycloakProtectedResourceRequirement>());
        Assert.Equal("document", requirement.Resource);
        Assert.Equal("read", requirement.Scope);
    }

    [Fact]
    public async Task ResourceWithColon_SplitsAtFirstSeparator()
    {
        // Documented behavior: first ':' after the prefix separates resource from scope.
        // kc:protected:document:42:read → resource "document", scope "42:read".
        var provider = Build();

        var policy = await provider.GetPolicyAsync("kc:protected:document:42:read");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<KeycloakProtectedResourceRequirement>());
        Assert.Equal("document", requirement.Resource);
        Assert.Equal("42:read", requirement.Scope);
    }

    [Fact]
    public async Task UnknownPolicyName_FallsThroughToDefaultProvider()
    {
        var provider = Build();

        var policy = await provider.GetPolicyAsync("policy-that-was-never-registered");

        Assert.Null(policy);
    }

    [Fact]
    public async Task MalformedProtectedResourceName_FallsThroughToDefaultProvider()
    {
        var provider = Build();

        var policy = await provider.GetPolicyAsync("kc:protected:resourceonly");

        Assert.Null(policy);
    }
}
