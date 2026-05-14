# KeycloakClient

An opinionated ASP.NET Core integration for the [Keycloak](https://www.keycloak.org/) Admin API and the [Phase Two webhook plugin](https://github.com/p2-inc/keycloak-events). Configure once via `appsettings.json`, then inject `IKeycloakClientService` and write event handlers.

## Features

- **Service-account `IKeycloakClientService`** — backed by client-credentials token management ([Duende.AccessTokenManagement](https://github.com/DuendeSoftware/Duende.AccessTokenManagement)). Works for background jobs and unauthenticated callers.
- **Optional delegated `IKeycloakClientService`** — forwards the incoming request's bearer token to the Admin API. Operations run with the caller's privileges.
- **Admin API coverage** — users (CRUD, search, count, reset-password, action emails), groups, user/group membership.
- **Webhook intake** — `app.UseKeycloakWebHooks()` mounts an endpoint, validates the shared secret in constant time, and dispatches typed events to your handlers.
- **Automatic registration** — registers (or removes) the webhook on startup based on `appsettings.json`.

## Requirements

- .NET 10 (`net10.0`)
- ASP.NET Core 10
- A Keycloak realm with a confidential client (for the service-account flow)
- The Phase Two webhook plugin installed in Keycloak (only if you use webhook features)

## Installation

```bash
dotnet add package KeycloakClient
```

## Configuration

Add a `Keycloak` section to `appsettings.json`:

```json
{
  "Keycloak": {
    "Server": "https://id.example.com",
    "Realm": "myrealm",
    "Resource": "myapp",
    "Agent": "MyApp/1.0",
    "Credentials": {
      "Secret": "REPLACE_ME"
    },
    "Webhook": {
      "Enabled": true,
      "URL": "https://myapp.example.com/webhooks/keycloak",
      "Secret": "shared-secret-with-keycloak"
    }
  }
}
```

| Key | Required | Description |
|---|---|---|
| `Server` | yes | Keycloak base URL (no trailing slash). |
| `Realm` | yes | Target realm. |
| `Resource` | yes | Confidential client id used for the service-account flow. |
| `Credentials.Secret` | yes (service-account flow) | Client secret. |
| `Agent` | no | User-Agent header sent on outgoing requests. |
| `Webhook.Enabled` | no | `true` to register, `false` to remove. Default `true`. |
| `Webhook.URL` | yes (webhooks) | Publicly reachable URL Keycloak will POST events to. |
| `Webhook.Secret` | no | Shared secret. When set, incoming events must carry header `X-Webhook-Secret: <value>`. |

## Quick start

### Registration

```csharp
using KeycloakClient.DependencyInjection;

const string DelegatedClientKey = "user";

builder.Services
    .AddKeycloakClient(builder.Configuration, c => c.AddDelegatedClient(DelegatedClientKey))
    .AddKeycloakWebHooks(c => c.AddEventHandler<MyUserCreatedHandler>());
```

`AddDelegatedClient` is optional — only register it if you want token-forwarding endpoints.

### Service-account client

Resolves the default `IKeycloakClientService`. Uses the configured client credentials for every call — no caller context needed.

```csharp
app.MapGet("/users", async (IKeycloakClientService client, CancellationToken ct) =>
    Results.Ok(await client.GetUsersAsync(cancellationToken: ct)));
```

### Delegated client

Resolves the keyed `IKeycloakClientService` registered via `AddDelegatedClient(key)`. The handler reads the incoming `Authorization: Bearer <token>` header and forwards it. Throws `InvalidOperationException` if called outside of an HTTP request scope or if the request has no bearer token — fail fast rather than silently issuing unauthorized requests.

```csharp
app.MapGet("/me/users", async (
        [FromKeyedServices("user")] IKeycloakClientService client,
        CancellationToken ct) =>
    Results.Ok(await client.GetUsersAsync(cancellationToken: ct)))
    .RequireAuthorization();
```

The forwarded token must carry the right realm-management roles for the operation to succeed (Keycloak enforces this server-side).

### Webhook endpoint

```csharp
app.UseKeycloakWebHooks(); // mounts POST /webhooks/keycloak by default
```

Pass a different path if you want: `app.UseKeycloakWebHooks("/hooks/kc");`. The endpoint validates `X-Webhook-Secret` against `Keycloak:Webhook:Secret` using a constant-time comparison.

### Event handlers

Derive from `KeycloakEventHandler`, declare the event types you care about, and implement `HandleAsync`:

```csharp
using KeycloakClient.Abstraction;
using KeycloakClient.Models;

public sealed class UserCreatedHandler(ILogger<UserCreatedHandler> logger) : KeycloakEventHandler
{
    public override IReadOnlySet<string> Types { get; } = new HashSet<string>
    {
        "access.REGISTER",
        "admin.USER-CREATE"
    };

    public override Task HandleAsync(KeycloakAdminEvent @event, CancellationToken ct)
    {
        logger.LogInformation("Got '{Type}' for {Username}", @event.Type, @event.Representation?.Username);
        return Task.CompletedTask;
    }
}
```

Register the handler when wiring webhooks: `c.AddEventHandler<UserCreatedHandler>()`. Handlers are invoked in parallel; exceptions from one handler don't affect others.

## Webhook lifecycle

On startup, the library:

1. Reads `Keycloak:Webhook` from configuration.
2. If `Url` is blank → skip.
3. Otherwise, find any existing Keycloak webhook with the same `Url` and **delete it**.
4. If `Enabled = true` → create a fresh webhook with the configured URL, secret, and the union of all `Types` from registered handlers.
5. If `Enabled = false` → stop after step 3 (a configuration toggle that also tidies up upstream).

Always delete-and-recreate keeps the secret in sync with `appsettings.json`, since Keycloak's webhook GET endpoint does not return secrets.

## Building & publishing

```bash
dotnet pack -c Release
dotnet nuget push bin/Release/KeycloakClient.<version>.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

## License

[MIT](LICENSE)
