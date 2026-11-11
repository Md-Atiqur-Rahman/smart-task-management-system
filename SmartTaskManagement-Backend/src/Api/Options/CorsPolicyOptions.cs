using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Options;

/// <summary>Allowed browser origins, bound from "Cors". Empty means no cross-origin access.</summary>
internal sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Default";

    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}

/// <summary>
/// Each origin must be an absolute http or https URI with no path, query, fragment or trailing
/// slash (the exact form browsers send in the Origin header). "*" is rejected: the policy allows
/// credentials, which forbids a wildcard origin.
/// </summary>
internal sealed class CorsPolicyOptionsValidator : IValidateOptions<CorsPolicyOptions>
{
    private const string Wildcard = "*";

    public ValidateOptionsResult Validate(string? name, CorsPolicyOptions options)
    {
        var failures = options.AllowedOrigins
            .Where(origin => !IsValidOrigin(origin))
            .Select(origin => string.Equals(origin, Wildcard, StringComparison.Ordinal)
                ? $"{CorsPolicyOptions.SectionName}:AllowedOrigins must not contain \"*\": the policy allows credentials."
                : $"{CorsPolicyOptions.SectionName}:AllowedOrigins contains \"{origin}\", which is not an origin "
                    + "(absolute http or https URI with no path, query, fragment or trailing slash).")
            .ToList();

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        // An origin is scheme://host[:port] exactly; anything after the authority (even "/") is rejected.
        return string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }
}
