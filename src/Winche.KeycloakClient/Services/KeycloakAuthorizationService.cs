using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Winche.KeycloakClient.Services;

public sealed class KeycloakAuthorizationService(
    IHttpClientFactory httpFactory,
    string httpClientName,
    IOptions<KeycloakClientOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IDistributedCache cache
) : IKeycloakAuthorizationService
{
    public async Task<bool> AuthorizeAsync(string resource, string scope, CancellationToken ct = default)
    {
        var ctx = httpContextAccessor.HttpContext
            ?? throw new KeycloakAuthorizationException("AuthorizeAsync called outside of an HTTP request scope.");

        var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new KeycloakAuthorizationException("No bearer token on the current request.");
        }
        var bearer = authHeader["Bearer ".Length..].Trim();

        var opts = options.Value;
        var ttl = opts.Authorization?.CacheDuration;
        string? cacheKey = ttl is null ? null : $"kc:authz:{HashBearer(bearer)}:{resource}:{scope}";

        if (cacheKey is not null)
        {
            var cached = await cache.GetStringAsync(cacheKey, ct);
            if (cached is not null) return cached == "1";
        }

        var granted = await EvaluateAsync(bearer, resource, scope, opts, ct);

        if (cacheKey is not null && ttl is { } duration)
        {
            await cache.SetStringAsync(
                cacheKey,
                granted ? "1" : "0",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration },
                ct);
        }

        return granted;
    }

    public async Task RequireAsync(string resource, string scope, CancellationToken ct = default)
    {
        if (!await AuthorizeAsync(resource, scope, ct))
            throw new KeycloakAuthorizationException($"Access denied for {resource}#{scope}.");
    }

    private async Task<bool> EvaluateAsync(string bearer, string resource, string scope, KeycloakClientOptions opts, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(httpClientName);
        var tokenEndpoint = $"/realms/{opts.Realm}/protocol/openid-connect/token";

        using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:uma-ticket",
            ["audience"] = opts.Resource,
            ["permission"] = $"{resource}#{scope}",
            ["response_mode"] = "decision",
            ["submit_request"] = "false"
        });

        using var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return false;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.True;
    }

    private static string HashBearer(string bearer)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(bearer));
        return Convert.ToHexString(bytes);
    }
}
