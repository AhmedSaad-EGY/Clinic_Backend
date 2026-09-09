using Clinic.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Infrastructure.Errors;

public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        int statusCode;
        string title;

        if (exception is DomainException)
        {
            statusCode = StatusCodes.Status400BadRequest;
            title = "تعذر تنفيذ الطلب لمخالفته إحدى قواعد العمل";
            LogDomainFailure(_logger, exception.Message);
        }
        else
        {
            statusCode = StatusCodes.Status500InternalServerError;
            title = "حدث خطأ غير متوقع";
            LogUnhandledException(_logger, exception);
        }

        httpContext.Response.StatusCode = statusCode;

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status400BadRequest
                ? exception.Message
                : null,
            Instance = httpContext.Request.Path,
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Warning,
        Message = "A domain rule rejected the request: {Message}")]
    private static partial void LogDomainFailure(ILogger logger, string message);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred while processing the request.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);
}
