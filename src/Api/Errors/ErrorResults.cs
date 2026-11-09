using System.Diagnostics;

using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.AspNetCore.Mvc;

namespace Datavanced.SmartTaskManagement.Api.Errors;

/// <summary>
/// The ONE place where an expected failure becomes HTTP: ErrorType -> status,
/// body = ProblemDetails (RFC 9457) with an "errorCode" extension, and an
/// "errors" map for validation failures.
/// </summary>
internal static class ErrorResults
{
    public const string ErrorCodeKey = "errorCode";
    public const string TraceIdKey = "traceId";
    public const string ProblemJson = "application/problem+json";

    /// <summary>The request's trace id, shared by every ProblemDetails response.</summary>
    public static string TraceIdFor(HttpContext httpContext) =>
        Activity.Current?.Id ?? httpContext.TraceIdentifier;

    public static ObjectResult ToProblem(this ControllerBase controller, Error error)
    {
        var status = StatusCodeFor(error.Type);
        ProblemDetails problem = error is ValidationError validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = TitleFor(error.Type);
        problem.Detail = error.Description;
        problem.Instance = controller.HttpContext.Request.Path;
        problem.Extensions[ErrorCodeKey] = error.Code;
        problem.Extensions[TraceIdKey] = TraceIdFor(controller.HttpContext);

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { ProblemJson },
        };
    }

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown error type."),
    };

    public static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "One or more validation errors occurred.",
        ErrorType.NotFound => "The resource was not found.",
        ErrorType.Conflict => "The request conflicts with the current state of the resource.",
        ErrorType.Forbidden => "Access to the resource is forbidden.",
        ErrorType.Unauthorized => "Authentication is required.",
        ErrorType.Unavailable => "The service is temporarily unavailable.",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown error type."),
    };
}
