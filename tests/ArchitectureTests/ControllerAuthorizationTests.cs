using System.Reflection;

using Datavanced.SmartTaskManagement.Api.Controllers.Samples;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>
/// Rule 5: every controller action declares [Authorize] or [AllowAnonymous]
/// (on the action or its controller); [AllowAnonymous] is allowed only in the
/// Sample namespace (until Phase 2 adds authentication). There is no
/// authorization fallback policy, so this rule is the only guard.
/// </summary>
public sealed class ControllerAuthorizationTests
{
    // Exactly the Sample controller's namespace; a ".Samples" segment elsewhere is not exempt.
    private static readonly string SampleNamespace = typeof(SamplesController).Namespace!;

    [Fact]
    public void EveryAction_HasAuthorizeOrAllowAnonymous()
    {
        var missing = Actions()
            .Where(a => !Has<AuthorizeAttribute>(a) && !Has<AllowAnonymousAttribute>(a))
            .Select(Describe)
            .ToList();

        missing.ShouldBeEmpty("Actions without [Authorize] or [AllowAnonymous]: " + string.Join(", ", missing));
    }

    [Fact]
    public void AllowAnonymous_OnlyInSampleNamespace()
    {
        var outsideSample = Actions()
            .Where(a => Has<AllowAnonymousAttribute>(a) && !IsSample(a.DeclaringType!))
            .Select(Describe)
            .ToList();

        outsideSample.ShouldBeEmpty("[AllowAnonymous] outside the Sample namespace: " + string.Join(", ", outsideSample));
    }

    private static IEnumerable<MethodInfo> Actions() =>
        Layers.Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    private static bool Has<TAttribute>(MethodInfo action)
        where TAttribute : Attribute =>
        action.GetCustomAttribute<TAttribute>() is not null
        || action.DeclaringType!.GetCustomAttribute<TAttribute>() is not null;

    private static bool IsSample(Type controller) =>
        string.Equals(controller.Namespace, SampleNamespace, StringComparison.Ordinal);

    private static string Describe(MethodInfo action) => $"{action.DeclaringType!.FullName}.{action.Name}";
}
