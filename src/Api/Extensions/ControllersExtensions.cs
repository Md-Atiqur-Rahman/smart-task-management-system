using System.Text.Json.Serialization;

using Contoso.TemplateApp.Api.Errors;
using Contoso.TemplateApp.Domain.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Contoso.TemplateApp.Api.Extensions;

internal static class ControllersExtensions
{
    /// <summary>
    /// Controllers. MVC's automatic 400 (malformed JSON, unbindable values) uses
    /// the same contract as FluentValidation failures: ValidationProblemDetails
    /// with errorCode "validation.failed".
    /// </summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = context =>
            {
                var factory = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();
                var problem = factory.CreateValidationProblemDetails(
                    context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
                problem.Extensions[ErrorResults.ErrorCodeKey] = ValidationError.ErrorCode;

                return new BadRequestObjectResult(problem)
                {
                    ContentTypes = { ErrorResults.ProblemJson },
                };
            });
        return services;
    }

    public static WebApplication UseApiControllers(this WebApplication app)
    {
        app.MapControllers();
        return app;
    }
}
