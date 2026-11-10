using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace SpendScope.Web.Services.Auth;

/// <summary>
/// Reads claims from a JWT payload for display and UI authorization only (spec 19.4). The
/// signature is not checked here: the server signs and validates every token it receives.
/// </summary>
public static class JwtClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";

    public static ClaimsPrincipal ToPrincipal(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        using var payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
        var claims = new List<Claim>();
        foreach (var property in payload.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                claims.AddRange(property.Value.EnumerateArray().Select(v => new Claim(property.Name, v.ToString())));
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "jwt", nameType: Name, roleType: Role));
    }

    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(Subject)?.Value, out var id) ? id : Guid.Empty;

    private static byte[] DecodeBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '=');
        return Convert.FromBase64String(s);
    }

    internal static string Base64UrlJson(object payload) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
