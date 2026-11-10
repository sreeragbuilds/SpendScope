using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SpendScope.Domain.Common;

namespace SpendScope.Api.Middleware;

/// <summary>Spec 18.1. Stack traces go to the log only, never into a response body.</summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Client went away: nothing useful to write.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, code, title) = exception switch
        {
            DomainException d => (StatusCodes.Status422UnprocessableEntity, "domain_rule_violation", d.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency_conflict",
                "This was changed by someone else. Reload and try again."),
            BadHttpRequestException b => (b.StatusCode, "bad_request", "The request could not be read."),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected with {Status} {Code}: {Message}", status, code, exception.Message);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Type = $"https://spendscope.dev/errors/{code.Replace('_', '-')}",
                Extensions = { ["errorCode"] = exception is DomainException de ? de.Code : code },
            },
        });
    }
}
