using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NetArchTest.Rules;

namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>
/// Rules 9 and 10: the DI style is decided by the project
/// (Configure* on IHostApplicationBuilder in Application/Infrastructure, Add*/Use*
/// in Api) and Program.cs registers nothing inline.
/// </summary>
public sealed class CompositionTests
{
    // Rule 9a: Application and Infrastructure expose Configure<Concern><TBuilder>(this TBuilder)
    // where TBuilder : IHostApplicationBuilder, and no Add*/Use* on IServiceCollection/WebApplication.
    [Fact]
    public void ApplicationAndInfrastructure_UseOnlyConfigureStyle()
    {
        var violations = ExtensionMethods(Layers.Application, Layers.Infrastructure)
            .Where(m => IsServiceCollectionOrAppExtension(m) || (IsHostBuilderExtension(m) && !IsConfigureMethod(m)))
            .Select(Describe)
            .ToList();

        ExtensionMethods(Layers.Application, Layers.Infrastructure).Count(IsHostBuilderExtension).ShouldBeGreaterThan(0);
        violations.ShouldBeEmpty("Wrong DI style in Application/Infrastructure: " + string.Join(", ", violations));
    }

    // Rule 9b: Api exposes only Add*(IServiceCollection) and Use*(WebApplication), never Configure*.
    [Fact]
    public void Api_UsesOnlyAddAndUseStyle()
    {
        var methods = ExtensionMethods(Layers.Api);
        var violations = methods
            .Where(m => IsHostBuilderExtension(m)
                || (FirstParameter(m) == typeof(IServiceCollection) && !m.Name.StartsWith("Add", StringComparison.Ordinal))
                || (FirstParameter(m) == typeof(WebApplication) && !m.Name.StartsWith("Use", StringComparison.Ordinal)))
            .Select(Describe)
            .ToList();

        methods.ShouldContain(m => FirstParameter(m) == typeof(IServiceCollection));
        methods.ShouldContain(m => FirstParameter(m) == typeof(WebApplication));
        violations.ShouldBeEmpty("Wrong DI style in Api: " + string.Join(", ", violations));
    }

    // Rule 9c: each Application/Infrastructure Configure* lives in
    // <Layer>/Configuration[/<Concern>]/HostApplicationBuilderExtensions.
    [Fact]
    public void ConfigureMethods_LiveInHostApplicationBuilderExtensions()
    {
        var violations = ExtensionMethods(Layers.Application, Layers.Infrastructure)
            .Where(IsHostBuilderExtension)
            .Where(m => m.DeclaringType!.Name != "HostApplicationBuilderExtensions"
                || !(m.DeclaringType.Namespace ?? string.Empty).Contains(".Configuration", StringComparison.Ordinal))
            .Select(Describe)
            .ToList();

        violations.ShouldBeEmpty("Configure* outside Configuration/<Concern>/HostApplicationBuilderExtensions: "
            + string.Join(", ", violations));
    }

    // Rule 10, attempt 1 (NetArchTest): Program depends on no registration API.
    // NetArchTest reads the IL of the top-level statements (see the positive control
    // in LayerTests), so a call like builder.Services.AddScoped(...) is visible.
    [Fact]
    public void Program_HasNoInlineServiceRegistration()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().HaveName(nameof(Program))
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions",
                "Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions",
                "Microsoft.Extensions.DependencyInjection.ServiceCollectionDescriptorExtensions",
                "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions",
                "Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions",
                "Microsoft.Extensions.DependencyInjection.OptionsBuilderExtensions",
                "Microsoft.Extensions.DependencyInjection.MvcServiceCollectionExtensions",
                "Microsoft.Extensions.DependencyInjection.HealthCheckServiceCollectionExtensions",
                "Microsoft.Extensions.DependencyInjection.ServiceDescriptor",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Program.cs registers services inline.");
    }

    private static List<MethodInfo> ExtensionMethods(params Assembly[] assemblies) =>
        [.. assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: true, IsSealed: true }) // static classes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.IsDefined(typeof(ExtensionAttribute), inherit: false))];

    private static Type FirstParameter(MethodInfo method) => method.GetParameters()[0].ParameterType;

    private static bool IsHostBuilderExtension(MethodInfo method)
    {
        var first = FirstParameter(method);
        return first == typeof(IHostApplicationBuilder)
            || (first.IsGenericParameter
                && first.GetGenericParameterConstraints().Contains(typeof(IHostApplicationBuilder)));
    }

    private static bool IsConfigureMethod(MethodInfo method) =>
        method.Name.StartsWith("Configure", StringComparison.Ordinal)
        && method.Name.Length > "Configure".Length
        && method.ReturnType == FirstParameter(method);

    private static bool IsServiceCollectionOrAppExtension(MethodInfo method)
    {
        var first = FirstParameter(method);
        return first == typeof(IServiceCollection) || first == typeof(WebApplication);
    }

    private static string Describe(MethodInfo method) => $"{method.DeclaringType!.FullName}.{method.Name}";
}
