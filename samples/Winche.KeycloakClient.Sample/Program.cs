using Winche.KeycloakClient.Authorization;
using Winche.KeycloakClient.DependencyInjection;
using Winche.KeycloakClient.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddKeycloakClient(builder.Configuration)
    .AddKeycloakAuthentication(builder.Configuration)
    .AddKeycloakAuthorization(builder.Configuration, c => c
        .AddRealmRolePolicy("admin")
        .AddResourceRolePolicy("editor")
        .AddProtectedResourcePolicy("can-read-doc", "document", "read"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Winche.KeycloakClient sample. See README for usage.");

app.MapGet("/admin/users", () => Results.Ok(new[] { "alice", "bob" }))
    .RequireAuthorization("admin");

app.MapGet("/documents", [ProtectedResource("document", "read")] () =>
    Results.Ok(new[] { new { id = 1, title = "Welcome" } }));

app.MapGet("/documents/{id:int}", async (
        int id,
        IKeycloakAuthorizationService authz,
        CancellationToken ct) =>
{
    await authz.RequireAsync($"document:{id}", "read", ct);
    return Results.Ok(new { id, title = $"Document #{id}" });
}).RequireAuthorization();

app.Run();
