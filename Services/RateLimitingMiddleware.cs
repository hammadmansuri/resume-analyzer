namespace resume_analyzer.Services;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingService _rateLimiter;

    public RateLimitingMiddleware(RequestDelegate next, RateLimitingService rateLimiter)
    {
        _next = next;
        _rateLimiter = rateLimiter;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Only rate limit POST requests to analysis endpoints
        if (HttpMethods.IsPost(context.Request.Method) && IsAnalysisEndpoint(context.Request.Path))
        {
            if (!_rateLimiter.IsAllowed(clientIp))
            {
                context.Response.StatusCode = 429; // Too Many Requests
                await context.Response.WriteAsync("Rate limit exceeded. Please try again later. (Max 5 requests per hour)");
                return;
            }
        }

        await _next(context);
    }

    private static bool IsAnalysisEndpoint(PathString path)
    {
        return path == "/" ||
            path.StartsWithSegments("/Index") ||
            path.StartsWithSegments("/api/analyze-resume");
    }
}
