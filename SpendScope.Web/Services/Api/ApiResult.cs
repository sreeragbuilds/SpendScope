using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpendScope.Web.Services.Api;

/// <summary>RFC 9457 ProblemDetails plus the SpendScope extensions.</summary>
public sealed class Problem
{
    public string? Title { get; set; }

    public int? Status { get; set; }

    public string? ErrorCode { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }

    public Dictionary<string, string[]>? ErrorCodes { get; set; }

    public string? TraceId { get; set; }

    [JsonIgnore]
    public TimeSpan? RetryAfter { get; set; }
}

/// <summary>
/// Spec 19.5: the client-side twin of the server's Result. Pages check <see cref="IsSuccess"/>
/// instead of catching exceptions. <see cref="Status"/> is 0 for network failures.
/// </summary>
public sealed class ApiResult<T>
{
    private ApiResult(int status, T? value, Problem? problem)
    {
        Status = status;
        Value = value;
        Problem = problem;
    }

    public int Status { get; }

    public T? Value { get; }

    public Problem? Problem { get; }

    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>The message for the first error on <paramref name="field"/> (camelCase), if any.</summary>
    public string? FieldError(string field) =>
        Problem?.Errors?.TryGetValue(field, out var messages) == true ? messages.FirstOrDefault() : null;

    public bool HasCode(string code) =>
        Problem?.ErrorCode == code || Problem?.ErrorCodes?.Values.Any(c => c.Contains(code)) == true;

    public static ApiResult<T> NetworkFailure() => new(0, default, null);

    public static ApiResult<T> Ok(T value) => new(200, value, null);

    public static ApiResult<T> Fail(int status, Problem? problem) => new(status, default, problem);

    public static async Task<ApiResult<T>> FromResponseAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        var status = (int)response.StatusCode;
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0 || typeof(T) == typeof(Unit))
            {
                return new ApiResult<T>(status, default, null);
            }

            return new ApiResult<T>(status, await response.Content.ReadFromJsonAsync<T>(ct), null);
        }

        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<Problem>(ct);
        }
        catch (JsonException)
        {
            // Not a ProblemDetails body (e.g. a proxy error page).
        }
        catch (NotSupportedException)
        {
        }

        problem ??= new Problem { Status = status };
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            problem.RetryAfter = delta;
        }

        return new ApiResult<T>(status, default, problem);
    }
}

/// <summary>Stands in for "no body" in <see cref="ApiResult{T}"/>.</summary>
public readonly record struct Unit;

/// <summary>User-facing messages per status (spec 18.3).</summary>
public static class ApiMessages
{
    public static string For<T>(ApiResult<T> result) => result.Status switch
    {
        0 => "Can't reach SpendScope. Check your connection.",
        400 => result.Problem?.Title ?? "Please check the highlighted fields.",
        401 => "Your session has ended. Please sign in again.",
        403 => "You don't have permission to do that.",
        404 => "Not found. It may have been deleted.",
        409 => result.Problem?.Title ?? "This was changed by someone else. Reload and try again.",
        422 => result.Problem?.Title ?? "That can't be done.",
        423 => LockedMessage(result.Problem?.RetryAfter),
        429 => "Too many requests. Wait a moment.",
        >= 500 => $"Something went wrong on our side.{(result.Problem?.TraceId is { } id ? $" Reference: {id}" : string.Empty)}",
        _ => result.Problem?.Title ?? "Something went wrong.",
    };

    public static string LockedMessage(TimeSpan? retryAfter) =>
        retryAfter is { } wait && wait > TimeSpan.Zero
            ? $"Account temporarily locked. Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} minute(s)."
            : "Account temporarily locked. Try again later.";
}
