using Contoso.TemplateApp.WeavingProbe;

namespace Contoso.TemplateApp.UnitTests.Domain;

/// <summary>
/// Real-run evidence of how NullGuard treats async, property and generic
/// members in the woven assemblies (same FodyWeavers.xml as src).
/// </summary>
public sealed class NullGuardBehaviourTests
{
    [Fact]
    public async Task NullableTask_ReturningNull_DoesNotThrow()
    {
        (await NullGuardProbe.NullableTaskAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task NullableValueTask_ReturningNull_DoesNotThrow()
    {
        (await NullGuardProbe.NullableValueTaskAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task NonNullableTask_ReturningNull_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => NullGuardProbe.NonNullableTaskAsync());
    }

    /// <summary>
    /// Characterization test of a KNOWN GAP in NullGuard.Fody 3.1.1: return values
    /// of ValueTask&lt;T&gt; are NOT checked (Task&lt;T&gt; is). Rule: non-nullable async
    /// results use Task&lt;T&gt;, not ValueTask&lt;T&gt; (.claude/rules/nullguard.md).
    /// If a NullGuard upgrade starts checking ValueTask, this test fails on purpose.
    /// </summary>
    [Fact]
    public async Task NonNullableValueTask_ReturningNull_IsNotChecked_KnownGap()
    {
        (await NullGuardProbe.NonNullableValueTaskAsync()).ShouldBeNull();
    }

    [Fact]
    public void NullableProperty_AcceptsAndReturnsNull()
    {
        var probe = new NullGuardProbe { NullableProperty = null };

        probe.NullableProperty.ShouldBeNull();
    }

    [Fact]
    public void NullableGenericReturn_ReturnsNull()
    {
        NullGuardProbe.NullableGenericReturn<string>().ShouldBeNull();
    }

    [Fact]
    public void NonNullableReturn_ReturningNull_Throws()
    {
        Should.Throw<InvalidOperationException>(
            () => NullGuardProbe.NonNullableReturn());
    }
}
