using Microsoft.AspNetCore.Identity;

namespace SpendScope.Infrastructure.Identity;

/// <summary>
/// Identity user (spec 8.1). Lives in Infrastructure, not Domain, because it derives from an
/// ASP.NET Core Identity type and ARCH-01 keeps Domain framework-free.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 50;

    public string DisplayName { get; set; } = string.Empty;

    public string DefaultCurrency { get; set; } = "USD";

    public DateTime CreatedAtUtc { get; set; }
}
