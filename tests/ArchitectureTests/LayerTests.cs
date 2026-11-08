using System.Xml.Linq;

using NetArchTest.Rules;

using TestResult = NetArchTest.Rules.TestResult;

namespace Contoso.TemplateApp.ArchitectureTests;

/// <summary>Rules 1-3: dependency rule, no persistence inside, UnitTests isolation.</summary>
public sealed class LayerTests
{
    // Rule 1: layer dependencies follow the dependency rule.
    [Fact]
    public void Domain_HasNoDependencyOn_OtherLayers()
    {
        var result = Types.InAssembly(Layers.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                Layers.ApplicationNamespace,
                Layers.InfrastructureNamespace,
                Layers.ApiNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void Application_HasNoDependencyOn_InfrastructureOrApi()
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot()
            .HaveDependencyOnAny(Layers.InfrastructureNamespace, Layers.ApiNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void Infrastructure_HasNoDependencyOn_Api()
    {
        var result = Types.InAssembly(Layers.Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(Layers.ApiNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    // Rule 2: Domain and Application have no EF Core or persistence reference.
    [Fact]
    public void DomainAndApplication_HaveNoDependencyOn_Persistence()
    {
        var result = Types.InAssemblies([Layers.Domain, Layers.Application])
            .ShouldNot()
            .HaveDependencyOnAny(Layers.PersistenceNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Fact]
    public void DomainAndApplication_DoNotReference_PersistenceAssemblies()
    {
        var referenced = new[] { Layers.Domain, Layers.Application }
            .SelectMany(a => a.GetReferencedAssemblies())
            .Select(a => a.Name ?? string.Empty);

        referenced.ShouldNotContain(
            name => Layers.PersistenceNamespaces.Any(
                p => name.StartsWith(p, StringComparison.Ordinal)));
    }

    // Rule 3: UnitTests does not reference Infrastructure or Api.
    // Checked on the project file: a compiled assembly only lists references it uses.
    [Fact]
    public void UnitTests_DoNotReference_InfrastructureOrApi()
    {
        var csproj = Path.Combine(RepositoryRoot(), "tests", "UnitTests", "UnitTests.csproj");

        var references = XDocument.Load(csproj)
            .Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(
                (string?)e.Attribute("Include") ?? string.Empty))
            .ToList();

        references.ShouldNotBeEmpty();
        references.ShouldNotContain("Infrastructure");
        references.ShouldNotContain("Api");
    }

    // Positive control: proves NetArchTest sees real dependencies, so the
    // "should not depend" rules above are not passing vacuously.
    [Fact]
    public void Control_NetArchTest_DetectsKnownDependency()
    {
        var dependents = Types.InAssembly(Layers.Api)
            .That()
            .HaveDependencyOn(Layers.ApplicationNamespace)
            .GetTypes();

        dependents.ShouldContain(t => t == typeof(Program));
    }

    private static string Failing(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root (*.slnx) not found.");
    }
}
