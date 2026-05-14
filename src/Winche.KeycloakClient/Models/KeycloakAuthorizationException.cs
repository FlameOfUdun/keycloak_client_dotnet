namespace Winche.KeycloakClient.Models;

public sealed class KeycloakAuthorizationException : Exception
{
    public KeycloakAuthorizationException(string message) : base(message) { }
    public KeycloakAuthorizationException(string message, Exception inner) : base(message, inner) { }
}
