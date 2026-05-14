using KeycloakClient.Models;

namespace KeycloakClient.Abstraction;

/// <summary>
/// Base class for handling Keycloak events. 
/// Implementations of this class should specify the types of events they are interested in and provide logic to handle those events.
/// </summary>
public abstract class KeycloakEventHandler
{
    /// <summary>
    /// Gets the set of type names associated with the current instance.
    /// </summary>
    /// <remarks>The returned set is read-only and reflects the types relevant to the instance. The contents
    /// and meaning of the types depend on the specific implementation.</remarks>
    public abstract IReadOnlySet<string> Types { get; }

    /// <summary>
    /// Asynchronously handles a Keycloak administrative event.
    /// </summary>
    /// <param name="event">The Keycloak administrative event to process. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous handling operation.</returns>
    public abstract Task HandleAsync(KeycloakAdminEvent @event, CancellationToken ct);
}
