using System.Globalization;
using System.Threading.RateLimiting;
using SpendScope.Features.Common;
using SpendScope.Infrastructure.Identity;

namespace SpendScope.Api.RateLimiting;

/// <summary>Spec 14.5. Limits come from the "RateLimiting" section so tests can raise them.</summary>
internal static class RateLimitSetup
{
    public static IServiceCollection AddSpendScopeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        var authStrict = section.GetValue("AuthStrictPerMinute", 5);
        var authRefresh = section.GetValue("AuthRefreshPerMinute", 30);
        var api = section.GetValue("ApiPerMinute", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Login, register, forgot/reset password: per client IP.
            options.AddPolicy(RateLimits.AuthStrict, ctx => PerMinute("ip:" + ClientIp(ctx), authStrict));
            options.AddPolicy(RateLimits.AuthRefresh, ctx => PerMinute("ip:" + ClientIp(ctx), authRefresh));

            // Every authenticated /api call: per user.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var userId = ctx.User.FindFirst(TokenClaims.Subject)?.Value;
                return userId is not null && ctx.Request.Path.StartsWithSegments("/api")
                    ? PerMinute("user:" + userId, api)
                    : RateLimitPartition.GetNoLimiter("anonymous");
            });

            options.OnRejected = async (context, ct) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                await Results.Problem(
                        title: "Too many requests. Wait a moment.",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        type: "https://spendscope.dev/errors/rate-limited",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "rate_limited" })
                    .ExecuteAsync(context.HttpContext);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> PerMinute(string key, int limit) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
