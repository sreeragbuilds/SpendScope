using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Contracts.Auth;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.Logout;

/// <summary>Spec 15.4. Idempotent: always 204. FR-AUTH-07.</summary>
public sealed class LogoutValidator : AbstractValidator<LogoutRequest>
{
    public LogoutValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("refresh_token_required").MaximumLength(200);
}

public sealed class LogoutHandler(IAppDbContext db, ITokenService tokens, ICurrentUser caller, IClock clock)
{
    public async Task HandleAsync(LogoutRequest request, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var now = clock.UtcNow;

        // Only the caller's own token can be revoked through this endpoint.
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.UserId == caller.UserId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }
}

public sealed class LogoutEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/logout", async (LogoutRequest request, LogoutHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(request, ct);
                return Results.NoContent();
            })
            .WithValidation<LogoutRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Auth")
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);
}
