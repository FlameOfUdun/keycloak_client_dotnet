using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Winche.KeycloakClient.Interfaces;
using Winche.KeycloakClient.Models;
using Microsoft.Extensions.Options;

namespace Winche.KeycloakClient.Services;

public class KeycloakClientService : IKeycloakClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _httpClientName;
    private readonly string _realm;
    private readonly JsonSerializerOptions _jsonOptions;

    public KeycloakClientService(IHttpClientFactory httpClientFactory, string httpClientName, IOptions<KeycloakClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _httpClientName = httpClientName;
        _realm = options.Value.Realm ?? throw new ArgumentNullException(nameof(options), "Keycloak realm is not configured");
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    private HttpClient HttpClient => _httpClientFactory.CreateClient(_httpClientName);

    private string AdminApi => $"/admin/realms/{_realm}";

    #region User Management

    public async Task CreateUserAsync(KeycloakUser user, CancellationToken ct = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"{AdminApi}/users", user, _jsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<KeycloakUser>> GetUsersAsync(KeycloakGetUsersParams? @params = null, CancellationToken ct = default)
    {
        var queryString = @params?.ToQueryString() ?? string.Empty;
        var response = await HttpClient.GetAsync($"{AdminApi}/users{queryString}", ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(_jsonOptions, ct) ?? [];
    }

    public async Task<int> GetUserCountAsync(KeycloakGetUsersParams? @params = null, CancellationToken ct = default)
    {
        var queryString = @params?.ToQueryString() ?? string.Empty;
        var response = await HttpClient.GetAsync($"{AdminApi}/users/count{queryString}", ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<int>(_jsonOptions, ct);
    }

    public async Task<KeycloakUser?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        var response = await HttpClient.GetAsync($"{AdminApi}/users/{userId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<KeycloakUser>(_jsonOptions, ct);
    }

    public async Task UpdateUserAsync(string userId, KeycloakUser user, CancellationToken ct = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"{AdminApi}/users/{userId}", user, _jsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        var response = await HttpClient.DeleteAsync($"{AdminApi}/users/{userId}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task ResetPasswordAsync(string userId, string password, bool temporary = false, CancellationToken ct = default)
    {
        var credential = new KeycloakCredential
        {
            Type = "password",
            Value = password,
            Temporary = temporary
        };

        var response = await HttpClient.PutAsJsonAsync($"{AdminApi}/users/{userId}/reset-password", credential, _jsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    #endregion

    #region Group Management

    public async Task<IReadOnlyList<KeycloakGroup>> GetGroupsAsync(CancellationToken ct = default)
    {
        var response = await HttpClient.GetAsync($"{AdminApi}/groups", ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<List<KeycloakGroup>>(_jsonOptions, ct) ?? [];
    }

    public async Task<KeycloakGroup?> GetGroupAsync(string groupId, CancellationToken ct = default)
    {
        var response = await HttpClient.GetAsync($"{AdminApi}/groups/{groupId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<KeycloakGroup>(_jsonOptions, ct);
    }

    public async Task<IReadOnlyList<KeycloakUser>> GetGroupMembersAsync(string groupId, int? first = null, int? max = null, CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (first.HasValue) queryParams.Add($"first={first.Value}");
        if (max.HasValue) queryParams.Add($"max={max.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var response = await HttpClient.GetAsync($"{AdminApi}/groups/{groupId}/members{queryString}", ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<List<KeycloakUser>>(_jsonOptions, ct) ?? [];
    }

    #endregion

    #region User-Group Membership

    public async Task AddUserToGroupAsync(string userId, string groupId, CancellationToken ct = default)
    {
        var response = await HttpClient.PutAsync($"{AdminApi}/users/{userId}/groups/{groupId}", null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken ct = default)
    {
        var response = await HttpClient.DeleteAsync($"{AdminApi}/users/{userId}/groups/{groupId}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<KeycloakGroup>> GetUserGroupsAsync(string userId, CancellationToken ct = default)
    {
        var response = await HttpClient.GetAsync($"{AdminApi}/users/{userId}/groups", ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<List<KeycloakGroup>>(_jsonOptions, ct) ?? [];
    }

    #endregion

    #region User Action Emails

    public async Task ExecuteActionsEmailAsync(
        string userId,
        IEnumerable<string> actions,
        string? clientId = null,
        string? redirectUri = null,
        int? lifespan = null,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (clientId is not null) queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
        if (redirectUri is not null) queryParams.Add($"redirect_uri={Uri.EscapeDataString(redirectUri)}");
        if (lifespan.HasValue) queryParams.Add($"lifespan={lifespan.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var response = await HttpClient.PutAsJsonAsync(
            $"{AdminApi}/users/{userId}/execute-actions-email{queryString}",
            actions,
            _jsonOptions,
            ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task SendVerifyEmailAsync(
        string userId,
        string? clientId = null,
        string? redirectUri = null,
        int? lifespan = null,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (clientId is not null) queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
        if (redirectUri is not null) queryParams.Add($"redirect_uri={Uri.EscapeDataString(redirectUri)}");
        if (lifespan.HasValue) queryParams.Add($"lifespan={lifespan.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        var response = await HttpClient.PutAsync(
            $"{AdminApi}/users/{userId}/send-verify-email{queryString}",
            null,
            ct);
        await EnsureSuccessAsync(response, ct);
    }

    #endregion

    #region Webhook Management

    public async Task<IReadOnlyList<KeycloakWebhook>> GetWebhooksAsync(CancellationToken ct = default)
    {
        var response = await HttpClient.GetAsync($"{RealmApi}/webhooks", ct);
        await EnsureSuccessAsync(response, ct);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions, ct);
        if (json.ValueKind == JsonValueKind.Array)
            return json.Deserialize<List<KeycloakWebhook>>(_jsonOptions) ?? [];

        return json.GetProperty("webhooks").Deserialize<List<KeycloakWebhook>>(_jsonOptions) ?? [];
    }

    public async Task CreateWebhookAsync(KeycloakWebhook webhook, CancellationToken ct = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"{RealmApi}/webhooks", webhook, _jsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task DeleteWebhookAsync(string webhookId, CancellationToken ct = default)
    {
        var response = await HttpClient.DeleteAsync($"{RealmApi}/webhooks/{webhookId}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task DeleteAllWebhooksAsync(CancellationToken ct = default)
    {
        var webhooks = await GetWebhooksAsync(ct);
        foreach (var hook in webhooks)
        {
            if (hook.Id is null) continue;
            var response = await HttpClient.DeleteAsync($"{RealmApi}/webhooks/{hook.Id}", ct);
            await EnsureSuccessAsync(response, ct);
        }
    }

    #endregion

    #region Helpers

    private string RealmApi => $"/realms/{_realm}";

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            throw new KeycloakApiException(response.StatusCode, content);
        }
    }

    #endregion
}

