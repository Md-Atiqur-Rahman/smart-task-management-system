namespace Contoso.TemplateApp.WeavingProbe;

/// <summary>
/// Members woven by NullGuard (Mode=NullableReferenceTypes,
/// ValidationFlags=Arguments|ReturnValues). Each one returns null on purpose;
/// the tests assert whether the woven code lets it through.
/// </summary>
public sealed class NullGuardProbe
{
    public string? NullableProperty { get; set; }

    public static async Task<string?> NullableTaskAsync()
    {
        await Task.Yield();
        return null;
    }

    public static async ValueTask<string?> NullableValueTaskAsync()
    {
        await Task.Yield();
        return null;
    }

    public static async Task<string> NonNullableTaskAsync()
    {
        await Task.Yield();
        return null!;
    }

    public static async ValueTask<string> NonNullableValueTaskAsync()
    {
        await Task.Yield();
        return null!;
    }

    public static TValue? NullableGenericReturn<TValue>() => default;

    public static string NonNullableReturn() => null!;
}
