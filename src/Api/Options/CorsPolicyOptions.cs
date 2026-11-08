namespace Contoso.TemplateApp.Api.Options;

/// <summary>Allowed browser origins, bound from "Cors". Empty means no cross-origin access.</summary>
internal sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Default";

    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}
