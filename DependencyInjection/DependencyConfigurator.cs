using KeycloakClient.Abstraction;
using KeycloakClient.Interfaces;
using KeycloakClient.Models;
using KeycloakClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycloakClient.DependencyInjection;

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