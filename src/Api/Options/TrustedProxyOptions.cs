using System.Net;
using System.Net.Sockets;

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

/// <summary>
/// Rejects non-IP or short-form IPv4 entries (such as "10") and, when the host's own
/// forwarded-headers switch is on, the trust-all configuration it would add.
/// </summary>
internal sealed class TrustedProxyOptionsValidator(bool hostForwardedHeadersEnabled)
    : IValidateOptions<TrustedProxyOptions>
{
    private const int Ipv4PartCount = 4;

    public ValidateOptionsResult Validate(string? name, TrustedProxyOptions options)
    {
        var failures = new List<string>();
        if (hostForwardedHeadersEnabled)
        {
            failures.Add(
                "ASPNETCORE_FORWARDEDHEADERS_ENABLED makes the host trust forwarded headers from every sender; "
                + $"remove it and list the proxies in {TrustedProxyOptions.SectionName}:KnownProxies instead.");
        }

        if (options.KnownProxies.Any(address => !TryParse(address, out _)))
        {
            failures.Add(
                $"{TrustedProxyOptions.SectionName}:KnownProxies contains values that are not IP addresses "
                + "(IPv4 needs four dotted parts).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Parses an address; IPv4-mapped IPv6 is normalised to IPv4. Short IPv4 forms are rejected.</summary>
    public static bool TryParse(string? value, out IPAddress address)
    {
        address = IPAddress.None;
        if (!IPAddress.TryParse(value, out var parsed))
        {
            return false;
        }

        if (parsed.AddressFamily == AddressFamily.InterNetwork
            && value!.Split('.').Length != Ipv4PartCount)
        {
            return false;
        }

        address = parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed;
        return true;
    }
}
