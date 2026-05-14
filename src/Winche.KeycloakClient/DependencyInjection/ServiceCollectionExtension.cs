using Duende.AccessTokenManagement;
using Duende.IdentityModel.Client;
using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Winche.KeycloakClient.DependencyInjection;

public static class ServiceCollectionExtension
{
    private const string CredentialsTokenClientName = "Winche.KeycloakClient.Credentials";
    private const string ServiceClientName = "Winche.KeycloakClient.Service.HttpClient";
    private const string TokenManagementBackChannelClientName = "Duende.AccessTokenManagement.BackChannelHttpClient";
    private const string AuthorizationHttpClientName = "Winche.KeycloakClient.Authorization.HttpClient";

    /// <summary>
    /// Adds Keycloak WebHooks services to the specified IServiceCollection.
    /// </summary>
    public static IServiceCollection AddKeycloakWebHooks(this IServiceCollection services, Action<WebhookDependencyConfigurator>? configure = null)
    {
        services.AddSingleton<IKeycloakWebhookService, KeycloakWebhookService>();
        services.AddHostedService<KeycloakWebhookRegistrationHostedService>();

        configure?.Invoke(new WebhookDependencyConfigurator(services));
        return services;
    }

    /// <summary>
    /// Adds and configures services required for Keycloak Admin API integration to the specified service collection.
    /// </summary>
    public static IServiceCollection AddKeycloakClient(this IServiceCollection services, IConfiguration configuration, Action<ClientDependencyConfigurator>? configure = null)
    {
        var section = configuration.GetSection(KeycloakClientOptions.SectionName);
        var options = section.Get<KeycloakClientOptions>()
            ?? throw new InvalidOperationException($"Failed to bind {KeycloakClientOptions.SectionName} configuration section.");

        services.Configure<KeycloakClientOptions>(section);

        services.AddDistributedMemoryCache();

        services.AddHttpClient(TokenManagementBackChannelClientName)
        .ConfigureHttpClient(client =>
        {
            if (!string.IsNullOrWhiteSpace(options.Agent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.Agent);
            }
        });

        services.AddClientCredentialsTokenManagement()
        .AddClient(CredentialsTokenClientName, client =>
        {
            client.TokenEndpoint = new Uri($"{options.Server}/realms/{options.Realm}/protocol/openid-connect/token");
            client.ClientId = ClientId.Parse(options.Resource);
            if (options.Credentials != null && !string.IsNullOrWhiteSpace(options.Credentials.Secret))
            {
                client.ClientSecret = ClientSecret.Parse(options.Credentials.Secret);
            }
            client.ClientCredentialStyle = ClientCredentialStyle.PostBody;
        });

        services.AddHttpClient(ServiceClientName, (sp, client) =>
        {
            client.BaseAddress = new Uri(options.Server);
            if (!string.IsNullOrWhiteSpace(options.Agent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.Agent);
            }
        })
        .AddClientCredentialsTokenHandler(ClientCredentialsClientName.Parse(CredentialsTokenClientName));

        services.AddSingleton<IKeycloakClientService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var options = sp.GetRequiredService<IOptions<KeycloakClientOptions>>();
            return new KeycloakClientService(factory, ServiceClientName, options);
        });

        configure?.Invoke(new ClientDependencyConfigurator(services));

        return services;
    }

    /// <summary>
    /// Configures JwtBearer authentication against the Keycloak realm specified in the
    /// <c>Keycloak</c> configuration section. Validates issuer, signing key, lifetime, and the
    /// <c>azp</c> claim against the configured <c>Resource</c>; flattens realm and resource roles
    /// into <see cref="System.Security.Claims.ClaimTypes.Role"/> claims.
    /// </summary>
    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration, Action<AuthenticationDependencyConfigurator>? configure = null)
    {
        var section = configuration.GetSection(KeycloakClientOptions.SectionName);
        var options = section.Get<KeycloakClientOptions>()
            ?? throw new InvalidOperationException($"Failed to bind {KeycloakClientOptions.SectionName} configuration section.");

        services.Configure<KeycloakClientOptions>(section);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o => KeycloakJwtBearerConfigurator.Configure(o, options));

        configure?.Invoke(new AuthenticationDependencyConfigurator(services));
        return services;
    }

    /// <summary>
    /// Adds Keycloak Authorization Services (UMA) integration: a dynamic policy provider for
    /// <c>kc:protected:{resource}:{scope}</c> policies, an authorization handler that evaluates
    /// permissions against the Keycloak token endpoint, and an <see cref="IKeycloakAuthorizationService"/>
    /// for imperative checks. Use the <paramref name="configure"/> callback to register named role
    /// and protected-resource policies.
    /// </summary>
    public static IServiceCollection AddKeycloakAuthorization(this IServiceCollection services, IConfiguration configuration, Action<AuthorizationDependencyConfigurator>? configure = null)
    {
        var section = configuration.GetSection(KeycloakClientOptions.SectionName);
        var options = section.Get<KeycloakClientOptions>()
            ?? throw new InvalidOperationException($"Failed to bind {KeycloakClientOptions.SectionName} configuration section.");

        services.Configure<KeycloakClientOptions>(section);
        services.AddHttpContextAccessor();
        services.AddDistributedMemoryCache();

        services.AddHttpClient(AuthorizationHttpClientName, (sp, client) =>
        {
            client.BaseAddress = new Uri(options.Server);
            if (!string.IsNullOrWhiteSpace(options.Agent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.Agent);
            }
        });

        services.AddSingleton<IKeycloakAuthorizationService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var opts = sp.GetRequiredService<IOptions<KeycloakClientOptions>>();
            var http = sp.GetRequiredService<IHttpContextAccessor>();
            var cache = sp.GetRequiredService<IDistributedCache>();
            return new KeycloakAuthorizationService(factory, AuthorizationHttpClientName, opts, http, cache);
        });

        services.AddSingleton<IAuthorizationHandler, KeycloakProtectedResourceHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, KeycloakPolicyProvider>();
        services.AddAuthorization();

        configure?.Invoke(new AuthorizationDependencyConfigurator(services, options));
        return services;
    }
}
