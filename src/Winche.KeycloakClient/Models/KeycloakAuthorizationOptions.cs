using Microsoft.Extensions.Configuration;

namespace Winche.KeycloakClient.Models;

public enum KeycloakDecisionStrategy
{
    Unanimous,
    Affirmative,
    Consensus
}

public sealed record KeycloakAuthorizationOptions
{
    [ConfigurationKeyName("CacheDuration")]
    public TimeSpan? CacheDuration { get; set; }

    [ConfigurationKeyName("DecisionStrategy")]
    public KeycloakDecisionStrategy DecisionStrategy { get; set; } = KeycloakDecisionStrategy.Unanimous;
}
