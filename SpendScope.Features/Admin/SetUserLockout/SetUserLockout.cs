using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Admin.SetUserLockout;

/// <summary>Spec 15.21. Locking revokes every refresh token of the target. FR-ADM-02 and 05.</summary>
public sealed class SetUserLockoutHandler(IUserAccounts accounts, IAppDbContext db, ICurrentUser caller, IClock clock)
{
    public async Task<Result> HandleAsync(Guid userId, bool locked, CancellationToken ct)
    {
        if (userId == caller.UserId)
        {
            return Error.Validation("cannot_lock_self", "You cannot lock or unlock your own account.", "id");
        }

        var result = await accounts.SetLockedAsync(userId, locked, ct);
        if (!result.IsSuccess || !locked)
        {
            return result;
        }

        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);

        return Result.Success;
    }
}

public sealed class SetUserLockoutEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/users/{id:guid}/lock", async (Guid id, SetUserLockoutHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, locked: true, ct)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Policies.RequireAdmin)
            .WithTags("Admin")
            .WithName("LockUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/admin/users/{id:guid}/unlock", async (Guid id, SetUserLockoutHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(id, locked: false, ct)).ToHttpResult(Results.NoContent))
            .RequireAuthorization(Policies.RequireAdmin)
            .WithTags("Admin")
            .WithName("UnlockUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
