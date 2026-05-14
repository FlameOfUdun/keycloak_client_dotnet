using Winche.KeycloakClient.Abstraction;
using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Winche.KeycloakClient.DependencyInjection;

/// <summary>
/// Provides a fluent API for configuring dependencies related to Keycloak event handling. 
/// </summary>
public sealed class WebhookDependencyConfigurator(IServiceCollection services)
{
    private readonly IServiceCollection _services = services;

    /// <summary>
    /// Registers an event handler of the specified type for Keycloak events using dependency injection.
    /// </summary>
    public WebhookDependencyConfigurator AddEventHandler<THandler>() where THandler : KeycloakEventHandler
    {
        _services.AddSingleton<KeycloakEventHandler, THandler>();
        return this;
    }
}

/// <summary>
/// 
/// </summary>
/// <param name="services"></param>
public sealed class ClientDependencyConfigurator(IServiceCollection services)
{
    private readonly IServiceCollection _services = services;

    /// <summary>
    /// Registers a delegated Keycloak client and its dependencies in the service collection using the specified key.
    /// </summary>
    public ClientDependencyConfigurator AddDelegatedClient(string key)
    {
        _services.AddHttpContextAccessor();
        _services.AddTransient<KeycloakTokenDelegationHandler>();

        var httpClientName = $"{key}.HttpClient";

        _services.AddHttpClient(httpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<KeycloakClientOptions>>().Value;
            client.BaseAddress = new Uri(options.Server);

            if (!string.IsNullOrWhiteSpace(options.Agent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.Agent);
            }
        })
        .AddHttpMessageHandler<KeycloakTokenDelegationHandler>();

        _services.AddKeyedSingleton<IKeycloakClientService>(key, (sp, _) =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var options = sp.GetRequiredService<IOptions<KeycloakClientOptions>>();
            return new KeycloakClientService(factory, httpClientName, options);
        });

        return this;
    }
}

/// <summary>
/// Reserved configurator for Keycloak authentication. Currently exposes no methods; placeholder for
/// future hooks such as registering custom JwtBearerEvents handlers.
/// </summary>
public sealed class AuthenticationDependencyConfigurator(IServiceCollection services)
{
    private readonly IServiceCollection _services = services;
}

/// <summary>
/// Fluent builder for Keycloak authorization policies. Role-policy methods use the role prefixes
/// configured in <see cref="KeycloakAuthenticationOptions"/> (resolved at registration time).
/// </summary>
public sealed class AuthorizationDependencyConfigurator
{
    private readonly IServiceCollection _services;
    private readonly string? _realmPrefix;
    private readonly string? _resourcePrefix;

    internal AuthorizationDependencyConfigurator(IServiceCollection services, KeycloakClientOptions options)
    {
        _services = services;
        _realmPrefix = options.Authentication?.RealmRolePrefix;
        _resourcePrefix = options.Authentication?.ResourceRolePrefix;
    }

    /// <summary>
    /// Registers a policy that requires the given realm role. Policy name defaults to the role name.
    /// </summary>
    public AuthorizationDependencyConfigurator AddRealmRolePolicy(string roleName, string? policyName = null)
    {
        var name = policyName ?? roleName;
        var requiredClaimValue = (_realmPrefix ?? string.Empty) + roleName;
        _services.Configure<AuthorizationOptions>(o =>
            o.AddPolicy(name, p => p.RequireAuthenticatedUser().RequireRole(requiredClaimValue)));
        return this;
    }

    /// <summary>
    /// Registers a policy that requires the given resource role. The <paramref name="clientId"/>
    /// parameter is informational in v1: roles from any client listed in
    /// <c>Authentication.AdditionalResourceClients</c> (plus the configured <c>Resource</c>) are
    /// flattened into the role claim by the claims transformer. To enforce strict client separation,
    /// set <c>Authentication.ResourceRolePrefix</c>.
    /// </summary>
    public AuthorizationDependencyConfigurator AddResourceRolePolicy(string roleName, string? clientId = null, string? policyName = null)
    {
        var name = policyName ?? roleName;
        var requiredClaimValue = (_resourcePrefix ?? string.Empty) + roleName;
        _services.Configure<AuthorizationOptions>(o =>
            o.AddPolicy(name, p => p.RequireAuthenticatedUser().RequireRole(requiredClaimValue)));
        return this;
    }

    /// <summary>
    /// Registers a named alias for a UMA protected-resource policy. Equivalent to
    /// <c>[Authorize(Policy = "kc:protected:{resource}:{scope}")]</c> but with a friendlier name.
    /// </summary>
    public AuthorizationDependencyConfigurator AddProtectedResourcePolicy(string policyName, string resource, string scope)
    {
        _services.Configure<AuthorizationOptions>(o =>
            o.AddPolicy(policyName, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new KeycloakProtectedResourceRequirement(resource, scope))));
        return this;
    }
}