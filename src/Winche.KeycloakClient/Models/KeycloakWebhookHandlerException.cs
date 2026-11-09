namespace Winche.KeycloakClient.Models;

/// <summary>One or more <see cref="Abstraction.KeycloakEventHandler"/>s failed; the webhook answers 500 so Keycloak retries.</summary>
public sealed class KeycloakWebhookHandlerException(string eventType, IReadOnlyList<Exception> failures)
    : AggregateException($"{failures.Count} handler(s) failed for Keycloak event '{eventType}'.", failures)
{
    public string EventType { get; } = eventType;
}
