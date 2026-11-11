namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>tests/WeavingProbe is test support (ADR 0008): no src project may reference it.</summary>
public sealed class WeavingProbeTests
{
    [Fact]
    public void SrcProjects_DoNotReference_WeavingProbe()
    {
        var violations = Layers.Src
            .Where(a => a.GetReferencedAssemblies()
                .Any(r => (r.Name ?? string.Empty).EndsWith(".WeavingProbe", StringComparison.Ordinal)))
            .Select(a => a.GetName().Name)
            .ToList();

        violations.ShouldBeEmpty("src assemblies referencing WeavingProbe: " + string.Join(", ", violations));
    }
}
