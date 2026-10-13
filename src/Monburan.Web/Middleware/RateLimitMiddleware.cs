using System.Threading.RateLimiting;

namespace Monburan.Web.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimiter _limiter;

    public RateLimitMiddleware(RequestDelegate next, RateLimiter limiter)
    {
        _next = next;
        _limiter = limiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var lease = await _limiter.AcquireAsync();
        if (lease.IsAcquired)
        {
            await _next(context);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("Too many requests. Please try again later.");
        }
    }
}

public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimit(this IApplicationBuilder builder, int permitsPerSecond = 100)
    {
        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = permitsPerSecond * 2,
            QueueLimit = permitsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = permitsPerSecond,
            AutoReplenishment = true
        });

        return builder.UseMiddleware<RateLimitMiddleware>(limiter);
    }
}
