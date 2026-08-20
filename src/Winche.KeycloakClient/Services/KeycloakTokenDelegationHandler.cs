using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Winche.KeycloakClient.Services;

public sealed class KeycloakTokenDelegationHandler(
    IHttpContextAccessor httpContextAccessor
) : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Delegated Keycloak client requires an active HttpContext; call it from within an HTTP request scope.");

        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Delegated Keycloak client requires a Bearer token on the incoming request.");

        var token = authHeader["Bearer ".Length..];
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }
}
