using System.Security.Claims;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;

namespace Winche.KeycloakClient.Tests;

public class KeycloakClaimsTransformerTests
{
    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Bearer"));

    private static KeycloakClientOptions Options(string resource = "myapp", KeycloakAuthenticationOptions? auth = null) =>
        new()
        {
            Server = "https://kc.test",
            Realm = "test",
            Resource = resource,
            Authentication = auth
        };

    [Fact]
    public void AzpMismatch_ReturnsFailureMessage()
    {
        var principal = BuildPrincipal(new Claim("azp", "other-app"));

        var failure = KeycloakClaimsTransformer.TransformCore(principal, Options(resource: "myapp"));

        Assert.NotNull(failure);
        Assert.Contains("other-app", failure);
        Assert.Contains("myapp", failure);
    }

    [Fact]
    public void AzpMatch_NoRoles_ReturnsNullAndEmitsNoRoleClaims()
    {
        var principal = BuildPrincipal(new Claim("azp", "myapp"));

        var failure = KeycloakClaimsTransformer.TransformCore(principal, Options());

        Assert.Null(failure);
        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void RealmRoles_FlattenedIntoRoleClaims()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("realm_access", "{\"roles\":[\"admin\",\"user\"]}"));

        var failure = KeycloakClaimsTransformer.TransformCore(principal, Options());

        Assert.Null(failure);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "admin", "user" }, roles);
    }

    [Fact]
    public void ResourceRoles_FlattenedIntoRoleClaims()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("resource_access", "{\"myapp\":{\"roles\":[\"editor\"]}}"));

        var failure = KeycloakClaimsTransformer.TransformCore(principal, Options());

        Assert.Null(failure);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Equal(new[] { "editor" }, roles);
    }

    [Fact]
    public void RolesSourceRealm_OnlyEmitsRealmRoles()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("realm_access", "{\"roles\":[\"admin\"]}"),
            new Claim("resource_access", "{\"myapp\":{\"roles\":[\"editor\"]}}"));

        var failure = KeycloakClaimsTransformer.TransformCore(
            principal,
            Options(auth: new KeycloakAuthenticationOptions { RolesSource = KeycloakRolesSource.Realm }));

        Assert.Null(failure);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Equal(new[] { "admin" }, roles);
    }

    [Fact]
    public void RolesSourceResource_OnlyEmitsResourceRoles()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("realm_access", "{\"roles\":[\"admin\"]}"),
            new Claim("resource_access", "{\"myapp\":{\"roles\":[\"editor\"]}}"));

        var failure = KeycloakClaimsTransformer.TransformCore(
            principal,
            Options(auth: new KeycloakAuthenticationOptions { RolesSource = KeycloakRolesSource.Resource }));

        Assert.Null(failure);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Equal(new[] { "editor" }, roles);
    }

    [Fact]
    public void RolePrefixes_AppliedIndependentlyToEachSource()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("realm_access", "{\"roles\":[\"admin\"]}"),
            new Claim("resource_access", "{\"myapp\":{\"roles\":[\"editor\"]}}"));

        var failure = KeycloakClaimsTransformer.TransformCore(
            principal,
            Options(auth: new KeycloakAuthenticationOptions
            {
                RolesSource = KeycloakRolesSource.RealmAndResource,
                RealmRolePrefix = "realm:",
                ResourceRolePrefix = "res:"
            }));

        Assert.Null(failure);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "realm:admin", "res:editor" }, roles);
    }

    [Fact]
    public void AdditionalResourceClients_AlsoContributeRoles()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("resource_access", "{\"myapp\":{\"roles\":[\"editor\"]},\"other-app\":{\"roles\":[\"viewer\"]}}"));

        var failure = KeycloakClaimsTransformer.TransformCore(
            principal,
            Options(auth: new KeycloakAuthenticationOptions
            {
                RolesSource = KeycloakRolesSource.Resource,
                AdditionalResourceClients = ["other-app"]
            }));

        Assert.Null(failure);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "editor", "viewer" }, roles);
    }

    [Fact]
    public void EmailGivenNameFamilyName_MappedToClaimTypes()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("email", "alice@example.com"),
            new Claim("given_name", "Alice"),
            new Claim("family_name", "Smith"));

        var failure = KeycloakClaimsTransformer.TransformCore(principal, Options());

        Assert.Null(failure);
        Assert.Equal("alice@example.com", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("Alice", principal.FindFirst(ClaimTypes.GivenName)?.Value);
        Assert.Equal("Smith", principal.FindFirst(ClaimTypes.Surname)?.Value);
    }

    [Fact]
    public void PreexistingClaimTypeEmail_NotOverwritten()
    {
        var principal = BuildPrincipal(
            new Claim("azp", "myapp"),
            new Claim("email", "from-kc@example.com"),
            new Claim(ClaimTypes.Email, "preexisting@example.com"));

        var failure = KeycloakClaimsTransformer.TransformCore(principal, Options());

        Assert.Null(failure);
        Assert.Equal("preexisting@example.com", principal.FindFirst(ClaimTypes.Email)?.Value);
    }
}
