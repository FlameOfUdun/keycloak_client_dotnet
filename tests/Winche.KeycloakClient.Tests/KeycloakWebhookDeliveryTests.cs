using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Winche.KeycloakClient.Abstraction;
using Winche.KeycloakClient.DependencyInjection;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;

namespace Winche.KeycloakClient.Tests;

public class KeycloakWebhookDeliveryTests
{
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"type":"admin.USER-DELETE","resourceId":"u-1"}""");

    private static string Sign(string secret, byte[] body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    [Fact]
    public void ValidSignature_IsAccepted() =>
        Assert.True(KeycloakWebhookSignature.IsValid("s3cret", Body, Sign("s3cret", Body).ToLowerInvariant()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutAConfiguredSecret_EverythingIsRejected(string? secret)
    {
        Assert.False(KeycloakWebhookSignature.IsValid(secret, Body, Sign("anything", Body)));
        Assert.False(KeycloakWebhookSignature.IsValid(secret, Body, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("abcd")]
    public void MissingOrMalformedSignatures_AreRejected(string? header) =>
        Assert.False(KeycloakWebhookSignature.IsValid("s3cret", Body, header));

    [Fact]
    public void SignatureForAnotherBodyOrSecret_IsRejected()
    {
        Assert.False(KeycloakWebhookSignature.IsValid("s3cret", Body, Sign("other", Body)));
        Assert.False(KeycloakWebhookSignature.IsValid("s3cret", Body, Sign("s3cret", Encoding.UTF8.GetBytes("{}"))));
    }

    [Fact]
    public async Task AFailingHandler_FailsTheEventSoKeycloakRetries_AfterTheOthersRan()
    {
        var ok = new Handler();
        var failing = new Handler { Fail = true };
        var service = Service(ok, failing);

        var ex = await Assert.ThrowsAsync<KeycloakWebhookHandlerException>(() => service.HandleEventAsync(Payload(), CancellationToken.None));

        Assert.Equal("admin.USER-DELETE", ex.EventType);
        Assert.Single(ex.InnerExceptions);
        Assert.Equal(1, ok.Calls);
        Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public async Task HandlersThatSucceed_CompleteNormally()
    {
        var handler = new Handler();
        await Service(handler).HandleEventAsync(Payload(), CancellationToken.None);
        Assert.Equal(1, handler.Calls);
    }

    private static JsonElement Payload() => JsonSerializer.Deserialize<JsonElement>(Body);

    private static KeycloakWebhookService Service(params KeycloakEventHandler[] handlers) =>
        new(null!, Options.Create(new KeycloakClientOptions()), NullLogger<KeycloakWebhookService>.Instance, handlers);

    private sealed class Handler : KeycloakEventHandler
    {
        public bool Fail { get; init; }
        public int Calls { get; private set; }
        public override IReadOnlySet<string> Types { get; } = new HashSet<string> { "admin.USER-DELETE" };

        public override Task HandleAsync(KeycloakWebhookEvent @event, CancellationToken ct)
        {
            Calls++;
            return Fail ? Task.FromException(new InvalidOperationException("db down")) : Task.CompletedTask;
        }
    }
}
