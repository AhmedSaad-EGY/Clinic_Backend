namespace Clinic.Api.Infrastructure.Diagnostics;

public sealed partial class RequestCorrelationMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = GetCorrelationId(context.Request);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            await _next(context);
        }
        finally
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                double elapsedMilliseconds =
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                LogRequestCompleted(
                    _logger,
                    context.Request.Method,
                    context.Request.Path.Value ?? string.Empty,
                    context.Response.StatusCode,
                    elapsedMilliseconds,
                    correlationId);
            }
        }
    }

    private static string GetCorrelationId(HttpRequest request)
    {
        string? suppliedValue = request.Headers[HeaderName].Count == 1
            ? request.Headers[HeaderName][0]
            : null;

        return Guid.TryParse(suppliedValue, out Guid suppliedId)
            ? suppliedId.ToString("N")
            : Guid.NewGuid().ToString("N");
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        SkipEnabledCheck = true,
        Message = "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMilliseconds:F1} ms " +
                  "with correlation ID {CorrelationId}.")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        double elapsedMilliseconds,
        string correlationId);
}
