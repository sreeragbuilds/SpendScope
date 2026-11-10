namespace SpendScope.Application.Common;

/// <summary>Maps to an HTTP status in the Features layer (spec 10.6).</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Locked,
    Unprocessable,
    Failure,
}

/// <param name="Code">Stable snake_case code returned to clients as <c>errorCode</c>.</param>
/// <param name="Field">For validation errors: the request property the error belongs to.</param>
/// <param name="RetryAfter">For <see cref="ErrorType.Locked"/>: how long until the client may retry.</param>
public sealed record Error(string Code, string Message, ErrorType Type, string? Field = null, TimeSpan? RetryAfter = null)
{
    public static Error Validation(string code, string message, string field) => new(code, message, ErrorType.Validation, field);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message, string? field = null) => new(code, message, ErrorType.Conflict, field);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Locked(string code, string message, TimeSpan? retryAfter) => new(code, message, ErrorType.Locked, RetryAfter: retryAfter);

    public static Error Unprocessable(string code, string message) => new(code, message, ErrorType.Unprocessable);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
