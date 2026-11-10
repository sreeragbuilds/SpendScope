using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.GetProfile;

/// <summary>Spec 15.7: GET and PUT /api/me. FR-AUTH-10.</summary>
public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName).ValidDisplayName();
        RuleFor(x => x.DefaultCurrency).SupportedCurrency();
    }
}

public sealed class ProfileHandler(IUserAccounts accounts, ICurrentUser caller)
{
    private static readonly Error Gone = Error.Unauthorized("user_not_found", "The signed-in user no longer exists.");

    public async Task<Result<UserProfileDto>> GetAsync(CancellationToken ct)
    {
        var user = await accounts.FindByIdAsync(caller.UserId, ct);
        return user is null ? Gone : ToDto(user);
    }

    public async Task<Result<UserProfileDto>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var updated = await accounts.UpdateProfileAsync(caller.UserId, request.DisplayName.Trim(), request.DefaultCurrency, ct);
        return updated.IsSuccess ? ToDto(updated.Value!) : updated.Error!;
    }

    private static UserProfileDto ToDto(UserAccount u) => new(u.Id, u.Email, u.DisplayName, u.DefaultCurrency, u.Roles);
}

public sealed class ProfileEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ProfileHandler handler, CancellationToken ct) =>
                (await handler.GetAsync(ct)).ToHttpResult(Results.Ok))
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Profile")
            .WithName("GetProfile")
            .Produces<UserProfileDto>();

        app.MapPut("/api/me", async (UpdateProfileRequest request, ProfileHandler handler, CancellationToken ct) =>
                (await handler.UpdateAsync(request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<UpdateProfileRequest>()
            .RequireAuthorization(Policies.RequireUser)
            .WithTags("Profile")
            .WithName("UpdateProfile")
            .Produces<UserProfileDto>();
    }
}
