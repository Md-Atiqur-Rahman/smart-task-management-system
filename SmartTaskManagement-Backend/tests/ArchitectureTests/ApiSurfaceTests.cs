namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>
/// Compensating control for the scoped CA1515 exception: the analyzer is off only
/// for src/Api/Controllers, so this test keeps every other Api type internal.
/// </summary>
public sealed class ApiSurfaceTests
{
    [Fact]
    public void PublicTypesInApi_OnlyInControllersOrContracts()
    {
        var allowedPrefixes = new[]
        {
            Layers.ApiNamespace + ".Controllers",
            Layers.ApiNamespace + ".Contracts",
        };

        var violations = Layers.Api.GetTypes()
            .Where(t => t.IsPublic)
            .Where(t => t != typeof(Program)) // entry point; WebApplicationFactory<Program> needs it
            .Where(t => !allowedPrefixes.Any(p =>
                t.Namespace == p || (t.Namespace?.StartsWith(p + ".", StringComparison.Ordinal) ?? false)))
            .Select(t => t.FullName)
            .ToList();

        violations.ShouldBeEmpty(
            "Public Api types outside Controllers/Contracts: " + string.Join(", ", violations));
    }
}
