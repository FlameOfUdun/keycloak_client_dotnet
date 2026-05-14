using Duende.AccessTokenManagement;
using Duende.IdentityModel.Client;
using KeycloakClient.Interfaces;
using KeycloakClient.Models;
using KeycloakClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycloakClient.DependencyInjection;

public static class ServiceCollectionExtension
{
    private const string CredentialsTokenClientName = "KeycloakClient.Credentials";
    private const string ServiceClientName = "KeycloakClient.Service.HttpClient";
    private const string TokenManagementBackChannelClientName = "Duende.AccessTokenManagement.BackChannelHttpClient";

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
}
