using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Winche.KeycloakClient.Abstraction;
using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Winche.KeycloakClient.Services;

namespace Winche.KeycloakClient.Tests;

public class KeycloakWebhookServiceTests
{
    private const string Url = "https://api-staging.paranote.net/webhooks/keycloak";

    [Fact]
    public async Task ExistingWebhookMatchingConfiguration_IsLeftInPlace()
    {
        var client = new RecordingClient(Existing("abc", true, "REGISTER", "UPDATE_PROFILE"));

        await Build(client).RegisterWebhookAsync(CancellationToken.None);

        Assert.Empty(client.Deleted);
        Assert.Empty(client.Created);
    }

    [Fact]
    public async Task ExistingWebhookMatchingInDifferentOrder_IsLeftInPlace()
    {
        var client = new RecordingClient(Existing("abc", true, "UPDATE_PROFILE", "REGISTER"));

        await Build(client).RegisterWebhookAsync(CancellationToken.None);

        Assert.Empty(client.Deleted);
        Assert.Empty(client.Created);
    }

    [Fact]
    public async Task ExistingWebhookWithDifferentEventTypes_IsReplaced()
    {
        var client = new RecordingClient(Existing("abc", true, "REGISTER"));

        await Build(client).RegisterWebhookAsync(CancellationToken.None);

        Assert.Equal(["abc"], client.Deleted);
        Assert.Single(client.Created);
    }

    [Fact]
    public async Task ExistingWebhookThatIsDisabled_IsReplaced()
    {
        var client = new RecordingClient(Existing("abc", false, "REGISTER", "UPDATE_PROFILE"));

        await Build(client).RegisterWebhookAsync(CancellationToken.None);

        Assert.Equal(["abc"], client.Deleted);
        Assert.Single(client.Created);
    }

    [Fact]
    public async Task RecreateRequested_ReplacesAnOtherwiseMatchingWebhook()
    {
        var client = new RecordingClient(Existing("abc", true, "REGISTER", "UPDATE_PROFILE"));

        await Build(client, recreate: true).RegisterWebhookAsync(CancellationToken.None);

        Assert.Equal(["abc"], client.Deleted);
        Assert.Single(client.Created);
    }

    [Fact]
    public async Task NoExistingWebhook_IsCreated()
    {
        var client = new RecordingClient();

        await Build(client).RegisterWebhookAsync(CancellationToken.None);

        Assert.Empty(client.Deleted);
        var created = Assert.Single(client.Created);
        Assert.Equal(Url, created.Url);
        Assert.True(created.Enabled);
        Assert.Equal(["REGISTER", "UPDATE_PROFILE"], created.EventTypes!.Order());
    }

    [Fact]
    public async Task WebhookDisabledInConfiguration_RemovesExistingAndCreatesNothing()
    {
        var client = new RecordingClient(Existing("abc", true, "REGISTER", "UPDATE_PROFILE"));

        await Build(client, enabled: false).RegisterWebhookAsync(CancellationToken.None);

        Assert.Equal(["abc"], client.Deleted);
        Assert.Empty(client.Created);
    }

    private static KeycloakWebhook Existing(string id, bool enabled, params string[] eventTypes) =>
        new() { Id = id, Enabled = enabled, Url = Url, EventTypes = eventTypes };

    private static KeycloakWebhookService Build(RecordingClient client, bool enabled = true, bool recreate = false) =>
        new(
            client,
            Options.Create(new KeycloakClientOptions
            {
                Webhook = new KeycloakWebhookOptions
                {
                    Enabled = enabled,
                    Url = Url,
                    Secret = "top-secret-phrase",
                    Recreate = recreate
                }
            }),
            NullLogger<KeycloakWebhookService>.Instance,
            [new StubHandler()]);

    private sealed class StubHandler : KeycloakEventHandler
    {
        public override IReadOnlySet<string> Types { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "REGISTER", "UPDATE_PROFILE" };

        public override Task HandleAsync(KeycloakWebhookEvent @event, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingClient(params KeycloakWebhook[] webhooks) : IKeycloakClientService
    {
        private readonly List<KeycloakWebhook> _webhooks = [.. webhooks];

        public List<string> Deleted { get; } = [];

        public List<KeycloakWebhook> Created { get; } = [];

        public Task<IReadOnlyList<KeycloakWebhook>> GetWebhooksAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<KeycloakWebhook>>(_webhooks);

        public Task CreateWebhookAsync(KeycloakWebhook webhook, CancellationToken ct)
        {
            Created.Add(webhook);
            return Task.CompletedTask;
        }

        public Task DeleteWebhookAsync(string webhookId, CancellationToken ct)
        {
            Deleted.Add(webhookId);
            _webhooks.RemoveAll(w => w.Id == webhookId);
            return Task.CompletedTask;
        }

        public Task DeleteAllWebhooksAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task CreateUserAsync(KeycloakUser user, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<KeycloakUser>> GetUsersAsync(KeycloakGetUsersParams parameters, CancellationToken ct) => throw new NotImplementedException();

        public Task<int> GetUserCountAsync(KeycloakGetUsersParams parameters, CancellationToken ct) => throw new NotImplementedException();

        public Task<KeycloakUser?> GetUserAsync(string userId, CancellationToken ct) => throw new NotImplementedException();

        public Task UpdateUserAsync(string userId, KeycloakUser user, CancellationToken ct) => throw new NotImplementedException();

        public Task DeleteUserAsync(string userId, CancellationToken ct) => throw new NotImplementedException();

        public Task ResetPasswordAsync(string userId, string password, bool temporary, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<KeycloakGroup>> GetGroupsAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task<KeycloakGroup?> GetGroupAsync(string groupId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<KeycloakUser>> GetGroupMembersAsync(string groupId, int? first, int? max, CancellationToken ct) => throw new NotImplementedException();

        public Task AddUserToGroupAsync(string userId, string groupId, CancellationToken ct) => throw new NotImplementedException();

        public Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<KeycloakGroup>> GetUserGroupsAsync(string userId, CancellationToken ct) => throw new NotImplementedException();

        public Task ExecuteActionsEmailAsync(string userId, IEnumerable<string> actions, string? clientId = null, string? redirectUri = null, int? lifespan = null, CancellationToken ct = default) => throw new NotImplementedException();

        public Task SendVerifyEmailAsync(string userId, string? clientId = null, string? redirectUri = null, int? lifespan = null, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
