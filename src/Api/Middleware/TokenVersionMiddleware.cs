using System.Globalization;

using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var status = ErrorResults.StatusCodeFor(error.Type);
        context.Response.StatusCode = status;
        await Results.Json(
            new ProblemDetails
            {
                Status = status,
                Title = ErrorResults.TitleFor(error.Type),
                Detail = error.Description,
                Instance = context.Request.Path,
                Extensions =
                {
                    [ErrorResults.ErrorCodeKey] = error.Code,
                    [ErrorResults.TraceIdKey] = ErrorResults.TraceIdFor(context),
                },
            },
            contentType: ErrorResults.ProblemJson,
            statusCode: status).ExecuteAsync(context);
    }
}
