using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;

namespace Winche.KeycloakClient.Tests;

public class KeycloakAuthorizationServiceTests
{
    private sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri, string? AuthorizationScheme, string? AuthorizationParameter, string Body);

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public List<CapturedRequest> Captures { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            // Read the body now — the SUT disposes the HttpRequestMessage after SendAsync returns,
            // which makes the content unreadable from the test afterward.
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            Captures.Add(new CapturedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));
            return respond(request);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler) { BaseAddress = new Uri("https://kc.test") };
    }

    private sealed class InMemoryDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public byte[]? Get(string key) => _store.GetValueOrDefault(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { Set(key, value, options); return Task.CompletedTask; }
    }

    private static HttpContext BuildHttpContext(string? bearer)
    {
        var ctx = new DefaultHttpContext();
        if (bearer is not null) ctx.Request.Headers.Authorization = $"Bearer {bearer}";
        return ctx;
    }

    private static HttpContextAccessor BuildAccessor(HttpContext? ctx) => new() { HttpContext = ctx };

    private static IOptions<KeycloakClientOptions> BuildOptions(TimeSpan? cacheDuration = null) =>
        Options.Create(new KeycloakClientOptions
        {
            Server = "https://kc.test",
            Realm = "test",
            Resource = "myapp",
            Authorization = cacheDuration is null ? null : new KeycloakAuthorizationOptions { CacheDuration = cacheDuration }
        });

    private static HttpResponseMessage JsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static KeycloakAuthorizationService BuildService(
        HttpMessageHandler handler,
        HttpContextAccessor accessor,
        IOptions<KeycloakClientOptions>? options = null,
        IDistributedCache? cache = null) =>
        new(new StubHttpClientFactory(handler), "test", options ?? BuildOptions(), accessor, cache ?? new InMemoryDistributedCache());

    [Fact]
    public async Task AuthorizeAsync_ThrowsWhenNoHttpContext()
    {
        var svc = BuildService(
            new StubMessageHandler(_ => JsonResponse("{}", HttpStatusCode.OK)),
            BuildAccessor(null));

        await Assert.ThrowsAsync<KeycloakAuthorizationException>(
            () => svc.AuthorizeAsync("document", "read", CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeAsync_ThrowsWhenNoBearer()
    {
        var svc = BuildService(
            new StubMessageHandler(_ => JsonResponse("{}", HttpStatusCode.OK)),
            BuildAccessor(BuildHttpContext(bearer: null)));

        await Assert.ThrowsAsync<KeycloakAuthorizationException>(
            () => svc.AuthorizeAsync("document", "read", CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeAsync_ReturnsTrue_WhenKeycloakGrants()
    {
        var svc = BuildService(
            new StubMessageHandler(_ => JsonResponse("{\"result\":true}")),
            BuildAccessor(BuildHttpContext("eyJ-token")));

        var result = await svc.AuthorizeAsync("document", "read", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task AuthorizeAsync_ReturnsFalse_WhenKeycloakDenies()
    {
        var svc = BuildService(
            new StubMessageHandler(_ => JsonResponse("{\"result\":false}")),
            BuildAccessor(BuildHttpContext("eyJ-token")));

        var result = await svc.AuthorizeAsync("document", "read", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task AuthorizeAsync_ReturnsFalse_OnNon2xx()
    {
        var svc = BuildService(
            new StubMessageHandler(_ => JsonResponse("nope", HttpStatusCode.Forbidden)),
            BuildAccessor(BuildHttpContext("eyJ-token")));

        var result = await svc.AuthorizeAsync("document", "read", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CacheEnabled_SecondCallHitsCache()
    {
        var handler = new StubMessageHandler(_ => JsonResponse("{\"result\":true}"));
        var cache = new InMemoryDistributedCache();
        var svc = BuildService(handler, BuildAccessor(BuildHttpContext("eyJ-token")), BuildOptions(TimeSpan.FromMinutes(5)), cache);

        var first = await svc.AuthorizeAsync("document", "read", CancellationToken.None);
        var second = await svc.AuthorizeAsync("document", "read", CancellationToken.None);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CacheDisabled_EveryCallHitsKeycloak()
    {
        var handler = new StubMessageHandler(_ => JsonResponse("{\"result\":true}"));
        var svc = BuildService(handler, BuildAccessor(BuildHttpContext("eyJ-token")));

        await svc.AuthorizeAsync("document", "read", CancellationToken.None);
        await svc.AuthorizeAsync("document", "read", CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task RequireAsync_ThrowsWhenDenied()
    {
        var svc = BuildService(
            new StubMessageHandler(_ => JsonResponse("{\"result\":false}")),
            BuildAccessor(BuildHttpContext("eyJ-token")));

        await Assert.ThrowsAsync<KeycloakAuthorizationException>(
            () => svc.RequireAsync("document", "read", CancellationToken.None));
    }

    [Fact]
    public async Task PostsExpectedUmaTicketRequest()
    {
        var handler = new StubMessageHandler(_ => JsonResponse("{\"result\":true}"));
        var svc = BuildService(handler, BuildAccessor(BuildHttpContext("eyJ-token")));

        await svc.AuthorizeAsync("document", "read", CancellationToken.None);

        var capture = Assert.Single(handler.Captures);
        Assert.Equal(HttpMethod.Post, capture.Method);
        Assert.EndsWith("/realms/test/protocol/openid-connect/token", capture.RequestUri!.PathAndQuery);
        Assert.Equal("Bearer", capture.AuthorizationScheme);
        Assert.Equal("eyJ-token", capture.AuthorizationParameter);

        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Auma-ticket", capture.Body);
        Assert.Contains("audience=myapp", capture.Body);
        Assert.Contains("permission=document%23read", capture.Body);
        Assert.Contains("response_mode=decision", capture.Body);
        Assert.Contains("submit_request=false", capture.Body);
    }
}
