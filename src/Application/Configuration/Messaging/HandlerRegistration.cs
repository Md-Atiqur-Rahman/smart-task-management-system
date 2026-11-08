using System.Reflection;

using Datavanced.SmartTaskManagement.Application.Abstractions.Behaviors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Datavanced.SmartTaskManagement.Application.Configuration.Messaging;

/// <summary>
/// Registers every concrete handler in an assembly and wraps it in the pipeline:
/// Logging -> Validation -> handler. Hand-written decoration (no MediatR, no Scrutor).
/// </summary>
internal static class HandlerRegistration
{
    private static readonly (Type Handler, Type Validation, Type Logging)[] Pipelines =
    [
        (typeof(ICommandHandler<,>),
            typeof(ValidationDecorator.CommandHandler<,>),
            typeof(LoggingDecorator.CommandHandler<,>)),
        (typeof(ICommandHandler<>),
            typeof(ValidationDecorator.CommandBaseHandler<>),
            typeof(LoggingDecorator.CommandBaseHandler<>)),
        (typeof(IQueryHandler<,>),
            typeof(ValidationDecorator.QueryHandler<,>),
            typeof(LoggingDecorator.QueryHandler<,>)),
    ];

    public static void Register(IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var implementation in handlerTypes)
        {
            foreach (var serviceType in implementation.GetInterfaces().Where(i => i.IsGenericType))
            {
                var pipeline = Pipelines.FirstOrDefault(
                    p => p.Handler == serviceType.GetGenericTypeDefinition());
                if (pipeline.Handler is null)
                {
                    continue;
                }

                var arguments = serviceType.GetGenericArguments();
                var validation = pipeline.Validation.MakeGenericType(arguments);
                var logging = pipeline.Logging.MakeGenericType(arguments);

                services.AddScoped(implementation);
                services.AddScoped(serviceType, provider =>
                {
                    var handler = provider.GetRequiredService(implementation);
                    var validated = ActivatorUtilities.CreateInstance(provider, validation, handler);
                    return ActivatorUtilities.CreateInstance(provider, logging, validated);
                });
            }
        }
    }
}
