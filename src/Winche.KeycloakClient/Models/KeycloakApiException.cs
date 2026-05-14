using System.Net;

namespace Winche.KeycloakClient.Models;

public sealed class KeycloakApiException(HttpStatusCode statusCode, string? responseContent) : Exception($"Keycloak API request failed with status {(int)statusCode} ({statusCode}): {responseContent}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseContent { get; } = responseContent;
}
