using System.Reflection;

using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Domain.Results;

using InfrastructureEntryPoint =
    Contoso.TemplateApp.Infrastructure.Configuration.HostApplicationBuilderExtensions;

namespace Contoso.TemplateApp.ArchitectureTests;

/// <summary>Assemblies and root namespaces of the four layers, derived from marker types (no literals).</summary>
internal static class Layers
{
    public static readonly Assembly Domain = typeof(Result).Assembly;
    public static readonly Assembly Application = typeof(ICommand).Assembly;
    public static readonly Assembly Infrastructure = typeof(InfrastructureEntryPoint).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;

    public static readonly Assembly[] Src = [Domain, Application, Infrastructure, Api];

    public static string DomainNamespace => Name(Domain);

    public static string ApplicationNamespace => Name(Application);

    public static string InfrastructureNamespace => Name(Infrastructure);

    public static string ApiNamespace => Name(Api);

    /// <summary>Namespaces that mean "persistence technology".</summary>
    public static readonly string[] PersistenceNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Data.SqlClient",
        "System.Data.SqlClient",
    ];

    private static string Name(Assembly assembly) =>
        assembly.GetName().Name ?? throw new InvalidOperationException("Assembly has no name.");
}
