using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Features.Common;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Api.Auth;

/// <summary>JWT bearer validation and authorization policies (spec 13.2, 13.8).</summary>
internal static class AuthSetup
{
    public static IServiceCollection AddSpendScopeAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from the validated JwtOptions so the issuing and validating sides cannot drift.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the token's own claim names ("sub", "role") rather than remapping them to
                // long WS-Federation URIs; ICurrentUser and the policies read these names.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = TokenClaims.Name,
                    RoleClaimType = TokenClaims.Role,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.RequireUser, p => p.RequireAuthenticatedUser().RequireRole(Roles.User, Roles.Admin))
            .AddPolicy(Policies.RequireAdmin, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        return services;
    }
}
