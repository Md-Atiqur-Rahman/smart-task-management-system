using System.Reflection;

using Contoso.TemplateApp.Domain.Abstractions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using NetArchTest.Rules;

namespace Contoso.TemplateApp.ArchitectureTests;

/// <summary>Rule 4: controllers stay thin; no repositories, UnitOfWork or DbContext.</summary>
public sealed class ControllerDependencyTests
{
    [Fact]
    public void Controllers_HaveNoDependencyOn_InfrastructureOrEfCore()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().Inherit(typeof(ControllerBase))
            .ShouldNot().HaveDependencyOnAny(
                Layers.InfrastructureNamespace,
                "Microsoft.EntityFrameworkCore",
                typeof(IUnitOfWork).FullName!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Violating controllers: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Controllers_DoNotInject_RepositoriesUnitOfWorkOrDbContext()
    {
        var injected = Layers.Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(c => c.GetConstructors().SelectMany(ctor => ctor.GetParameters())
                .Concat(c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()))
                .Select(p => (Controller: c.Name, p.ParameterType)))
            .Where(x => IsForbidden(x.ParameterType))
            .Select(x => $"{x.Controller}: {x.ParameterType.Name}")
            .ToList();

        injected.ShouldBeEmpty("Forbidden dependencies: " + string.Join(", ", injected));
    }

    // Controllers must inject handler INTERFACES; a concrete handler would skip
    // the Logging -> Validation pipeline.
    [Fact]
    public void Controllers_InjectHandlerInterfaces_NotConcreteHandlers()
    {
        var handlerInterfaces = new[]
        {
            typeof(Application.Abstractions.Messaging.ICommandHandler<>),
            typeof(Application.Abstractions.Messaging.ICommandHandler<,>),
            typeof(Application.Abstractions.Messaging.IQueryHandler<,>),
        };

        var concrete = Layers.Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(c => c.GetConstructors().SelectMany(ctor => ctor.GetParameters())
                .Concat(c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()))
                .Select(p => (Controller: c.Name, p.ParameterType)))
            .Where(x => x.ParameterType is { IsClass: true }
                && x.ParameterType.GetInterfaces().Any(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            .Select(x => $"{x.Controller}: {x.ParameterType.Name}")
            .ToList();

        concrete.ShouldBeEmpty("Concrete handlers injected (pipeline bypassed): " + string.Join(", ", concrete));
    }

    private static bool IsForbidden(Type type) =>
        type == typeof(IUnitOfWork)
        || typeof(DbContext).IsAssignableFrom(type)
        || type.Name.EndsWith("Repository", StringComparison.Ordinal);
}
