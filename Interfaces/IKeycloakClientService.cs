using KeycloakClient.Models;

namespace KeycloakClient.Interfaces;

public interface IKeycloakClientService
{
    #region User Management

    Task CreateUserAsync(KeycloakUser user, CancellationToken ct);
    Task<IReadOnlyList<KeycloakUser>> GetUsersAsync(KeycloakGetUsersParams parameters, CancellationToken ct);
    Task<int> GetUserCountAsync(KeycloakGetUsersParams parameters, CancellationToken ct);
    Task<KeycloakUser?> GetUserAsync(string userId, CancellationToken ct);
    Task UpdateUserAsync(string userId, KeycloakUser user, CancellationToken ct);
    Task DeleteUserAsync(string userId, CancellationToken ct);
    Task ResetPasswordAsync(string userId, string password, bool temporary, CancellationToken ct);

    #endregion

    #region Group Management
    Task<IReadOnlyList<KeycloakGroup>> GetGroupsAsync(CancellationToken ct);
    Task<KeycloakGroup?> GetGroupAsync(string groupId, CancellationToken ct);
    Task<IReadOnlyList<KeycloakUser>> GetGroupMembersAsync(string groupId, int? first, int? max, CancellationToken ct);

    #endregion

    #region User-Group Membership

    Task AddUserToGroupAsync(string userId, string groupId, CancellationToken ct);
    Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken ct);
    Task<IReadOnlyList<KeycloakGroup>> GetUserGroupsAsync(string userId, CancellationToken ct);

    #endregion

    #region User Action Emails

    Task ExecuteActionsEmailAsync(string userId, IEnumerable<string> actions, string? clientId = null, string? redirectUri = null, int? lifespan = null, CancellationToken ct = default);
    Task SendVerifyEmailAsync(string userId, string? clientId = null, string? redirectUri = null, int? lifespan = null, CancellationToken ct = default);

    #endregion

    #region Webhook Management

    Task<IReadOnlyList<KeycloakWebhook>> GetWebhooksAsync(CancellationToken ct);
    Task CreateWebhookAsync(KeycloakWebhook webhook, CancellationToken ct);
    Task DeleteWebhookAsync(string webhookId, CancellationToken ct);
    Task DeleteAllWebhooksAsync(CancellationToken ct);

    #endregion
}
