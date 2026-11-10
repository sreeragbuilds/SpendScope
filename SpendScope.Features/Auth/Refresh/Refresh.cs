using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Contracts.Common;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.Refresh;

/// <summary>Spec 13.5 and 15.3: rotation with reuse detection. FR-AUTH-06.</summary>
public sealed class RefreshValidator : AbstractValidator<RefreshRequest>
{
    public RefreshValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("refresh_token_required").MaximumLength(200);
}

public sealed class RefreshHandler(
    IAppDbContext db,
    IUserAccounts accounts,
    ITokenService tokens,
    ICurrentUser caller,
    IClock clock,
    ILogger<RefreshHandler> logger)
{
    private static readonly Error Invalid = Error.Unauthorized("invalid_refresh_token", "The session has expired. Sign in again.");

    public async Task<Result<TokenResponse>> HandleAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
        {
            return Invalid;
        }

        if (stored.IsRevoked)
        {
            // A rotated token came back: assume it was stolen and end every session for this user.
            await RevokeAllAsync(stored.UserId, now, ct);
            logger.LogWarning("Refresh token reuse detected for user {UserId} from ip {Ip}", stored.UserId, caller.IpAddress);
            return Error.Unauthorized(ErrorCodes.RefreshTokenReused, "This session was ended for security reasons. Sign in again.");
        }

        if (stored.IsExpired(now))
        {
            return Invalid;
        }

        var user = await accounts.FindByIdAsync(stored.UserId, ct);
        if (user is null || user.IsLocked)
        {
            await RevokeAllAsync(stored.UserId, now, ct);
            return Invalid;
        }

        var (response, replacement) = AuthTokens.Issue(user, tokens, now, caller.IpAddress);

        // NFR-08: revoke-old and insert-new succeed or fail together. The conditional update makes
        // two concurrent refreshes with the same token race safely: only one can win.
        await using var transaction = await db.BeginTransactionAsync(ct);
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.ReplacedByTokenHash, replacement.TokenHash),
                ct);

        if (rotated == 0)
        {
            await transaction.RollbackAsync(ct);
            await RevokeAllAsync(stored.UserId, now, ct);
            logger.LogWarning("Concurrent refresh token reuse for user {UserId} from ip {Ip}", stored.UserId, caller.IpAddress);
            return Error.Unauthorized(ErrorCodes.RefreshTokenReused, "This session was ended for security reasons. Sign in again.");
        }

        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return response;
    }

    private Task<int> RevokeAllAsync(Guid userId, DateTime now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
}

public sealed class RefreshEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/refresh", async (RefreshRequest request, RefreshHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<RefreshRequest>()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.AuthRefresh)
            .WithTags("Auth")
            .WithName("Refresh")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}
