using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Contoso.TemplateApp.Api.Errors;

/// <summary>
/// Last line of defence: every unhandled exception becomes a generic 500
/// ProblemDetails. Details (message, stack trace) go ONLY to the log.
/// A cancellation caused by the client going away is not a server error: it is
/// logged at Information and nothing is written (499, the client is gone).
/// The framework's exception-handler middleware already treats that case the same
/// way before calling handlers; this guard keeps the rule if the handler is reached.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public const string InternalErrorCode = "internal_error";
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogClientAborted(logger, httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Instance = httpContext.Request.Path,
            Extensions = { [ErrorResults.ErrorCodeKey] = InternalErrorCode },
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request {Method} {Path} was aborted by the client")]
    private static partial void LogClientAborted(ILogger logger, string method, PathString path);
}
