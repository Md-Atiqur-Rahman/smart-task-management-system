using System.Net;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Options;

/// <summary>
/// Reverse proxies whose X-Forwarded-For header is trusted, bound from "ForwardedHeaders".
/// Empty (the default) means direct connections: the header is never honoured.
/// </summary>
internal sealed class TrustedProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Exact IP addresses of the proxies (not networks).</summary>
    public IReadOnlyList<string> KnownProxies { get; init; } = [];
}

internal sealed class TrustedProxyOptionsValidator : IValidateOptions<TrustedProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, TrustedProxyOptions options)
    {
        var invalid = options.KnownProxies
            .Where(address => !IPAddress.TryParse(address, out _))
            .ToList();
        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{TrustedProxyOptions.SectionName}:KnownProxies contains values that are not IP addresses.");
    }
}
