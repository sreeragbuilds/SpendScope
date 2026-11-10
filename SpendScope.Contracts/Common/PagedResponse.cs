namespace SpendScope.Contracts.Common;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Stable error codes clients can switch on (the <c>errorCode</c> ProblemDetails extension).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string RefreshTokenReused = "refresh_token_reused";
    public const string ReplacementRequired = "replacement_required";
    public const string SystemCategoryReadOnly = "system_category_read_only";
}
