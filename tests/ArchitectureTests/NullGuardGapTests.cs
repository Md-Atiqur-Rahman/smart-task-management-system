using System.Reflection;
using System.Threading.RateLimiting;

namespace Datavanced.SmartTaskManagement.ArchitectureTests;

/// <summary>
/// Closes the known NullGuard gap (ADR 0008): NullGuard does not check the result
/// of a non-nullable ValueTask&lt;T&gt;. src code must use Task&lt;T&gt; for a
/// non-nullable reference result, or ValueTask&lt;T?&gt;. Value-type results
/// (ValueTask&lt;bool&gt;, ValueTask&lt;InterceptionResult&lt;int&gt;&gt;) cannot be null
/// and are allowed. Overrides of framework members are exempt: the framework fixes their
/// signature (for example PartitionedRateLimiter.AcquireAsyncCore), so Task&lt;T&gt; is not an
/// option there. Uses the compiler's nullability metadata (NullabilityInfoContext).
/// </summary>
public sealed class NullGuardGapTests
{
    [Fact]
    public void Src_HasNoNonNullableReferenceValueTask()
    {
        var violations = Layers.Src
            .SelectMany(a => a.GetTypes())
            .SelectMany(Violations)
            .ToList();

        violations.ShouldBeEmpty("Non-nullable ValueTask<T> (unchecked by NullGuard): " + string.Join(", ", violations));
    }

    // Positive control: the detection sees a non-nullable ValueTask<string> and
    // accepts ValueTask<string?> and ValueTask<bool>.
    [Fact]
    public void Control_DetectsNonNullableValueTaskOnly()
    {
        Violations(typeof(ControlSamples)).ShouldBe([$"{typeof(ControlSamples).FullName}.{nameof(ControlSamples.NonNullable)}"]);
    }

    // The exemption covers only overrides of members declared outside src.
    [Fact]
    public void Control_IgnoresOverridesOfFrameworkMembers()
    {
        Violations(typeof(FrameworkOverrideSample)).ShouldBeEmpty();
    }

    private static IEnumerable<string> Violations(Type type)
    {
        var nullability = new NullabilityInfoContext();
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType.IsGenericType
                && m.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)
                && !m.ReturnType.GenericTypeArguments[0].IsValueType
                && nullability.Create(m.ReturnParameter).GenericTypeArguments[0].ReadState == NullabilityState.NotNull
                && !OverridesFrameworkMember(m))
            .Select(m => $"{type.FullName}.{m.Name}");
    }

    private static bool OverridesFrameworkMember(MethodInfo method)
    {
        var declaringType = method.GetBaseDefinition().DeclaringType;
        return declaringType is not null
            && declaringType != method.DeclaringType
            && !Layers.Src.Contains(declaringType.Assembly);
    }

    private static class ControlSamples
    {
        public static ValueTask<string> NonNullable() => ValueTask.FromResult(string.Empty);

        public static ValueTask<string?> Nullable() => ValueTask.FromResult<string?>(null);

        public static ValueTask<bool> ValueType() => ValueTask.FromResult(true);
    }

    // Abstract: only the declared override matters, the type is never instantiated.
    private abstract class FrameworkOverrideSample : PartitionedRateLimiter<object>
    {
        protected override ValueTask<RateLimitLease> AcquireAsyncCore(
            object resource, int permitCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
