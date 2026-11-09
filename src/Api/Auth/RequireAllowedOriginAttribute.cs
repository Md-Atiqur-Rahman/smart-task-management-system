using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Api.Options;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Auth;

/// <summary>
/// CSRF check for refresh and logout: a request whose Origin header is present and not one
/// of Cors:AllowedOrigins gets 403 auth.origin_not_allowed. A missing Origin is allowed
/// (curl, Postman; SameSite=Strict already keeps browsers from sending the cookie cross-site).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequireAllowedOriginAttribute : Attribute, IAsyncActionFilter
{
    internal static readonly Error OriginNotAllowed =
        Error.Forbidden("auth.origin_not_allowed", "The request origin is not allowed.");

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Headers.TryGetValue("Origin", out var origin) && origin.Count > 0)
        {
            var allowed = http.RequestServices.GetRequiredService<IOptions<CorsPolicyOptions>>().Value.AllowedOrigins;
            if (!allowed.Contains(origin.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                var problem = ErrorResults.ToProblemDetails(http, OriginNotAllowed);
                context.Result = new ObjectResult(problem)
                {
                    StatusCode = problem.Status,
                    ContentTypes = { ErrorResults.ProblemJson },
                };
                return;
            }
        }

        await next();
    }
}
