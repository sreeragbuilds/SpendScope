namespace SpendScope.Features.Common;

/// <summary>Authorization policy names (spec 7.3). Registered by the Api.</summary>
public static class Policies
{
    public const string RequireUser = "RequireUser";
    public const string RequireAdmin = "RequireAdmin";
}

/// <summary>Rate limiter policy names (spec 14.5). Registered by the Api.</summary>
public static class RateLimits
{
    public const string AuthStrict = "auth-strict";
    public const string AuthRefresh = "auth-refresh";
    public const string Api = "api-default";
}
