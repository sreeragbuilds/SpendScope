using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Contracts.Common;
using SpendScope.Domain.Common;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.Login;

/// <summary>Spec 15.2. FR-AUTH-04 and 05.</summary>
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithErrorCode("email_invalid").MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("password_required").MaximumLength(256);
    }
}

public sealed class LoginHandler(
    IUserAccounts accounts,
    IAppDbContext db,
    ITokenService tokens,
    ICurrentUser caller,
    IClock clock,
    ILogger<LoginHandler> logger)
{
    public async Task<Result<TokenResponse>> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var check = await accounts.CheckPasswordAsync(request.Email.Trim(), request.Password, ct);

        switch (check.Status)
        {
            case PasswordCheckStatus.LockedOut:
                logger.LogWarning("Login rejected: account locked (ip {Ip})", caller.IpAddress);
                return Error.Locked(ErrorCodes.AccountLocked, "Account temporarily locked. Try again later.", check.RetryAfter);

            case PasswordCheckStatus.InvalidCredentials:
                // Same message for unknown email and wrong password (spec 15.2).
                logger.LogWarning("Login failed: invalid credentials (ip {Ip})", caller.IpAddress);
                return Error.Unauthorized(ErrorCodes.InvalidCredentials, "Email or password is incorrect.");
        }

        var (response, row) = AuthTokens.Issue(check.Account!, tokens, clock.UtcNow, caller.IpAddress);
        db.RefreshTokens.Add(row);
        await db.SaveChangesAsync(ct);

        return response;
    }
}

public sealed class LoginEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", async (LoginRequest request, LoginHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<LoginRequest>()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.AuthStrict)
            .WithTags("Auth")
            .WithName("Login")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);
}
