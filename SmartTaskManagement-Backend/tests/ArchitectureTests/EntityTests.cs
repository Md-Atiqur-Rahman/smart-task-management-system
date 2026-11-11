using System.Reflection;

using Datavanced.SmartTaskManagement.Domain.Primitives;

namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>Rule 8: entities have no public setters (init included); state changes go through methods.</summary>
public sealed class EntityTests
{
    [Fact]
    public void Entities_HaveNoPublicSetters()
    {
        var entities = Layers.Domain.GetTypes()
            .Where(t => t is { IsClass: true } && IsEntity(t))
            .ToList();

        var violations = entities
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        entities.ShouldNotBeEmpty();
        violations.ShouldBeEmpty("Public setters on entities: " + string.Join(", ", violations));
    }

    private static bool IsEntity(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }
}
