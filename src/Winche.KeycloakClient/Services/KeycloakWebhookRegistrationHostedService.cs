using Winche.KeycloakClient.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Winche.KeycloakClient.Services;

internal sealed class KeycloakWebhookRegistrationHostedService(
    IServiceProvider serviceProvider,
    ILogger<KeycloakWebhookRegistrationHostedService> logger
) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<KeycloakWebhookRegistrationHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var webhookService = scope.ServiceProvider.GetRequiredService<IKeycloakWebhookService>();
            await webhookService.RegisterWebhookAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Keycloak] Failed to register webhook on startup.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
