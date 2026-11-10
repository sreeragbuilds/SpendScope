namespace SpendScope.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record UpdateProfileRequest(string DisplayName, string DefaultCurrency);

public sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType = "Bearer");

public sealed record UserDto(Guid Id, string Email, string DisplayName);

public sealed record UserProfileDto(Guid Id, string Email, string DisplayName, string DefaultCurrency, IReadOnlyList<string> Roles);

/// <summary>Allowed values for a user's default currency (spec 15.7).</summary>
public static class SupportedCurrencies
{
    public static IReadOnlyList<string> All { get; } = ["USD", "EUR", "GBP", "INR", "JPY", "AUD", "CAD"];

    public static bool IsSupported(string? code) => code is not null && All.Contains(code, StringComparer.Ordinal);
}

/// <summary>Password policy from spec 13.3, shared so the client can show a live checklist.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MinUniqueChars = 4;

    public static IReadOnlyList<(string Rule, bool Met)> Evaluate(string? password)
    {
        var p = password ?? string.Empty;
        return
        [
            ($"At least {MinLength} characters", p.Length >= MinLength),
            ("An uppercase letter", p.Any(char.IsUpper)),
            ("A lowercase letter", p.Any(char.IsLower)),
            ("A digit", p.Any(char.IsDigit)),
            ("A symbol", p.Any(c => !char.IsLetterOrDigit(c))),
            ($"At least {MinUniqueChars} different characters", p.Distinct().Count() >= MinUniqueChars),
        ];
    }

    public static bool IsSatisfied(string? password) => Evaluate(password).All(r => r.Met);
}
