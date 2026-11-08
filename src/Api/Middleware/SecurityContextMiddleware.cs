using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.Api.Middleware;

/// <summary>
/// The ONLY caller of ISecurityContextSetter: runs once per request, after JWT
/// validation, and hands the validated principal to the scoped security context.
/// </summary>
internal sealed class SecurityContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ISecurityContextSetter securityContextSetter)
    {
        securityContextSetter.SetPrincipal(context.User);
        return next(context);
    }
}
