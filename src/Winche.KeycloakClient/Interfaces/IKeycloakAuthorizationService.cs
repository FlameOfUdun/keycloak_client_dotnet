namespace Winche.KeycloakClient.Interfaces;

public interface IKeycloakAuthorizationService
{
    Task<bool> AuthorizeAsync(string resource, string scope, CancellationToken ct);

    Task RequireAsync(string resource, string scope, CancellationToken ct);
}
