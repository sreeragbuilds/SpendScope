using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SpendScope.Infrastructure.Identity;

/// <summary>Bound from "Jwt" and validated at startup (spec 13.2, 22.3).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "SpendScope";

    [Required]
    public string Audience { get; set; } = "SpendScope.Clients";

    /// <summary>HMAC-SHA256 key. At least 32 characters (256 bits). Comes from secrets, never appsettings.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 14;

    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;

    /// <summary>Used by both the issuer (TokenService) and the validator (JwtBearer) so they always agree.</summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
