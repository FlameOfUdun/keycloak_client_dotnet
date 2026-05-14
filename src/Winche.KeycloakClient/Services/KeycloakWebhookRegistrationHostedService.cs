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
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var webhookService = scope.ServiceProvider.GetRequiredService<IKeycloakWebhookService>();
            await webhookService.RegisterWebhooksAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Keycloak] Failed to register webhook on startup.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
