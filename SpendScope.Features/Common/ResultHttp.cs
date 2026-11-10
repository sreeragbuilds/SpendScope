using System.Globalization;
using Microsoft.AspNetCore.Http;
using SpendScope.Application.Common;
using SpendScope.Contracts.Common;

namespace SpendScope.Features.Common;

/// <summary>
/// Maps <see cref="Result{T}"/> to HTTP (spec 10.6) as RFC 9457 ProblemDetails with an
/// <c>errorCode</c> extension. Lives in Features (not Api as appendix A.3 suggests) because the
/// slice endpoints call it and Features may not reference Api.
/// </summary>
public static class ResultHttp
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value!) : result.Error!.ToProblem();

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        if (error.Type == ErrorType.Validation)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { [error.Field ?? "request"] = [error.Message] },
                title: "One or more validation errors occurred.",
                type: TypeUri("validation"),
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = ErrorCodes.ValidationFailed,
                    ["errorCodes"] = new Dictionary<string, string[]> { [error.Field ?? "request"] = [error.Code] },
                });
        }

        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Locked => StatusCodes.Status423Locked,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?> { ["errorCode"] = error.Code };
        if (error.Field is not null)
        {
            extensions["field"] = error.Field;
        }

        var problem = Results.Problem(
            title: error.Message,
            statusCode: status,
            type: TypeUri(error.Code.Replace('_', '-')),
            extensions: extensions);

        return error.RetryAfter is { } retry && retry > TimeSpan.Zero
            ? new WithRetryAfter(problem, retry)
            : problem;
    }

    private static string TypeUri(string slug) => $"https://spendscope.dev/errors/{slug}";

    /// <summary>Adds a Retry-After header (seconds) to a problem result, as 423 responses require.</summary>
    private sealed class WithRetryAfter(IResult inner, TimeSpan retryAfter) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            return inner.ExecuteAsync(httpContext);
        }
    }
}
