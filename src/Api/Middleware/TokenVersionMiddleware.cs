using System.Globalization;

using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.AspNetCore.Authorization;

namespace Datavanced.SmartTaskManagement.Api.Middleware;

/// <summary>
/// Runs right after authentication (ADR 0014). Rejects tokens whose version is behind the
/// user's current version (401) and fails closed with 503 when the shared store is down.
/// Anonymous endpoints and unauthenticated requests are skipped.
/// </summary>
internal sealed class TokenVersionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenVersionChecker checker)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        var user = context.User;
        if (!Guid.TryParse(user.FindFirst(SecurityClaimTypes.UserId)?.Value, out var userId)
            || !int.TryParse(
                user.FindFirst(SecurityClaimTypes.TokenVersion)?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var tokenVersion))
        {
            await WriteAsync(context, AuthErrors.TokenRevoked);
            return;
        }

        var result = await checker.CheckAsync(userId, tokenVersion, context.RequestAborted);
        switch (result)
        {
            case TokenVersionCheck.Valid:
                await next(context);
                break;
            case TokenVersionCheck.Unavailable:
                await WriteAsync(context, AuthErrors.Unavailable);
                break;
            default:
                await WriteAsync(context, AuthErrors.TokenRevoked);
                break;
        }
    }

    private static async Task WriteAsync(HttpContext context, Error error)
    {
        var problem = ErrorResults.ToProblemDetails(context, error);
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await Results.Json(problem, contentType: ErrorResults.ProblemJson, statusCode: problem.Status)
            .ExecuteAsync(context);
    }
}
