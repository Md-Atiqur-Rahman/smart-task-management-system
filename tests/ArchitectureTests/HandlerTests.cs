using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>Rules 6 and 7: handler naming/interfaces; query handlers use read repositories only.</summary>
public sealed class HandlerTests
{
    private static readonly Type[] CommandHandlerInterfaces = [typeof(ICommandHandler<>), typeof(ICommandHandler<,>)];
    private static readonly Type[] QueryHandlerInterfaces = [typeof(IQueryHandler<,>)];

    // Rule 6
    [Fact]
    public void UseCaseHandlers_FollowNamingAndLocation()
    {
        var violations = UseCaseHandlers()
            .Where(t => !t.IsSealed
                || !t.Name.EndsWith("Handler", StringComparison.Ordinal)
                || !(t.Namespace ?? string.Empty).Contains(".Features.", StringComparison.Ordinal)
                || (Implements(t, CommandHandlerInterfaces) && !t.Name.EndsWith("CommandHandler", StringComparison.Ordinal))
                || (Implements(t, QueryHandlerInterfaces) && !t.Name.EndsWith("QueryHandler", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();

        UseCaseHandlers().ShouldNotBeEmpty();
        violations.ShouldBeEmpty("Handlers breaking naming/location: " + string.Join(", ", violations));
    }

    [Fact]
    public void ClassesNamedHandler_ImplementAHandlerInterface()
    {
        var violations = Layers.Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false })
            .Where(t => t.Name.EndsWith("CommandHandler", StringComparison.Ordinal)
                ? !Implements(t, CommandHandlerInterfaces)
                : t.Name.EndsWith("QueryHandler", StringComparison.Ordinal) && !Implements(t, QueryHandlerInterfaces))
            .Select(t => t.FullName)
            .ToList();

        violations.ShouldBeEmpty("Wrong handler interface: " + string.Join(", ", violations));
    }

    // Rule 7
    [Fact]
    public void QueryHandlers_InjectOnlyReadRepositories()
    {
        var queryHandlers = UseCaseHandlers().Where(t => Implements(t, QueryHandlerInterfaces)).ToList();

        var violations = queryHandlers
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters())
                .Where(p => IsWriteSide(p.ParameterType))
                .Select(p => $"{t.Name}({p.ParameterType.Name})"))
            .ToList();

        queryHandlers.ShouldNotBeEmpty();
        violations.ShouldBeEmpty("Query handlers with write-side dependencies: " + string.Join(", ", violations));
    }

    /// <summary>Concrete handlers of use cases (the pipeline decorators are excluded).</summary>
    private static List<Type> UseCaseHandlers() =>
        [.. Layers.Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false })
            .Where(t => Implements(t, [.. CommandHandlerInterfaces, .. QueryHandlerInterfaces]))];

    private static bool Implements(Type type, Type[] openGenericInterfaces) =>
        type.GetInterfaces().Any(i => i.IsGenericType && openGenericInterfaces.Contains(i.GetGenericTypeDefinition()));

    private static bool IsWriteSide(Type type) =>
        type == typeof(IUnitOfWork)
        || typeof(DbContext).IsAssignableFrom(type)
        || (type.Name.EndsWith("Repository", StringComparison.Ordinal)
            && !type.Name.EndsWith("ReadRepository", StringComparison.Ordinal));
}
