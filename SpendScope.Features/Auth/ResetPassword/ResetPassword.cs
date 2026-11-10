using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.ResetPassword;

/// <summary>Spec 15.6. Tokens are single use and expire after an hour. Revokes all sessions.</summary>
public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Token).NotEmpty().WithErrorCode("invalid_token").MaximumLength(2000);
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}

public sealed class ResetPasswordHandler(IUserAccounts accounts, IAppDbContext db, IClock clock)
{
    public async Task<Result> HandleAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var reset = await accounts.ResetPasswordAsync(request.Email.Trim(), request.Token, request.NewPassword, ct);
        if (!reset.IsSuccess)
        {
            return reset.Error!;
        }

        var userId = reset.Value;
        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

        return Result.Success;
    }
}

public sealed class ResetPasswordEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/reset-password", async (ResetPasswordRequest request, ResetPasswordHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(Results.NoContent))
            .WithValidation<ResetPasswordRequest>()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.AuthStrict)
            .WithTags("Auth")
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status204NoContent);
}
