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

namespace SpendScope.Features.Auth.ChangePassword;

/// <summary>Spec 15.5. On success every refresh token is revoked. FR-AUTH-08.</summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithErrorCode("password_required");
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}

public sealed class ChangePasswordHandler(IUserAccounts accounts, IAppDbContext db, ICurrentUser caller, IClock clock)
{
    public async Task<Result> HandleAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await accounts.ChangePasswordAsync(caller.UserId, request.CurrentPassword, request.NewPassword, ct);
        if (!result.IsSuccess)
        {
            return result;
        }

        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == caller.UserId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

        return Result.Success;
    }
}

public sealed class ChangePasswordEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/change-password", async (ChangePasswordRequest request, ChangePasswordHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(Results.NoContent))
            .WithValidation<ChangePasswordRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Auth")
            .WithName("ChangePassword")
            .Produces(StatusCodes.Status204NoContent);
}
