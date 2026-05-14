using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace KeycloakClient.Services;

public sealed class KeycloakTokenDelegationHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<KeycloakTokenDelegationHandler> logger
) : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<KeycloakTokenDelegationHandler> _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("Delegated Keycloak client requires an active HttpContext; call it from within an HTTP request scope.");

        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Delegated Keycloak client requires a Bearer token on the incoming request.");

        var token = authHeader["Bearer ".Length..];
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogDebug("Forwarding user token to Keycloak Admin API: {Uri}", request.RequestUri);

        return await base.SendAsync(request, ct);
    }
}
