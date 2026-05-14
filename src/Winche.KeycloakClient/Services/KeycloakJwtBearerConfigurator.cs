using Winche.KeycloakClient.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Winche.KeycloakClient.Services;

internal static class KeycloakJwtBearerConfigurator
{
    public static void Configure(JwtBearerOptions o, KeycloakClientOptions options)
    {
        var authority = $"{options.Server.TrimEnd('/')}/realms/{options.Realm}";
        o.Authority = authority;
        o.RequireHttpsMetadata = authority.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

        var auth = options.Authentication ?? new KeycloakAuthenticationOptions();

        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateIssuerSigningKey = true,
            ValidateAudience = auth.ValidateAudience,
            ValidAudience = options.Resource,
            ValidateLifetime = true,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };

        var existing = o.Events ?? new JwtBearerEvents();
        var prior = existing.OnTokenValidated;
        existing.OnTokenValidated = async ctx =>
        {
            KeycloakClaimsTransformer.Transform(ctx, options);
            if (prior is not null) await prior(ctx);
        };
        o.Events = existing;
    }
}
