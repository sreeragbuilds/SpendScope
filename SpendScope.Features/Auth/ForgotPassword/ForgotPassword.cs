using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SpendScope.Application.Abstractions;
using SpendScope.Application.Common;
using SpendScope.Contracts.Auth;
using SpendScope.Features.Common;

namespace SpendScope.Features.Auth.ForgotPassword;

/// <summary>
/// Spec 15.6. The response is identical whether or not the email exists, so the endpoint cannot
/// be used to discover accounts. FR-AUTH-09.
/// </summary>
public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordValidator() => RuleFor(x => x.Email).ValidEmail();
}

public sealed class ForgotPasswordHandler(IUserAccounts accounts, IEmailSender email, IOptions<AppOptions> app)
{
    public const string AcceptedMessage = "If an account exists for that email, a reset link has been sent.";

    public async Task HandleAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var reset = await accounts.CreatePasswordResetTokenAsync(request.Email.Trim(), ct);
        if (reset is not { } r)
        {
            return;
        }

        var link = $"{app.Value.PublicBaseUrl.TrimEnd('/')}/reset-password" +
                   $"?email={Uri.EscapeDataString(r.Account.Email)}&token={Uri.EscapeDataString(r.Token)}";

        await email.SendAsync(
            r.Account.Email,
            "Reset your SpendScope password",
            $"Hi {r.Account.DisplayName},\n\nReset your password with this link (valid for 1 hour):\n{link}\n\n" +
            "If you did not ask for this, you can ignore this email.",
            ct);
    }
}

public sealed record ForgotPasswordAccepted(string Message);

public sealed class ForgotPasswordEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest request, ForgotPasswordHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(request, ct);
                return Results.Accepted(value: new ForgotPasswordAccepted(ForgotPasswordHandler.AcceptedMessage));
            })
            .WithValidation<ForgotPasswordRequest>()
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.AuthStrict)
            .WithTags("Auth")
            .WithName("ForgotPassword")
            .Produces<ForgotPasswordAccepted>(StatusCodes.Status202Accepted);
}
