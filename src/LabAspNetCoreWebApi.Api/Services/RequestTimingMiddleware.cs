namespace LabAspNetCoreWebApi.Api.Services;

public sealed class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        long start = TimeProvider.System.GetTimestamp();
        await next(context);
        TimeSpan elapsed = TimeProvider.System.GetElapsedTime(start);

        logger.LogInformation(
            "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs} ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);
    }
}
