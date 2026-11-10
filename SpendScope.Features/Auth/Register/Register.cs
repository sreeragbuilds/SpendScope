using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.Register;

/// <summary>Spec 15.1. FR-AUTH-01 to 03.</summary>
public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.DisplayName).ValidDisplayName();
    }
}

public sealed class RegisterHandler(IUserAccounts accounts)
{
    public async Task<Result<UserDto>> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(request.Email.Trim(), request.Password, request.DisplayName.Trim(), ct);
        return result.IsSuccess
            ? new UserDto(result.Value!.Id, result.Value.Email, result.Value.DisplayName)
            : result.Error!;
    }
}

public sealed class RegisterEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/register", async (RegisterRequest request, RegisterHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(dto => Results.Created("/api/me", dto)))
            .WithValidation<RegisterRequest>()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.AuthStrict)
            .WithTags("Auth")
            .WithName("Register")
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
