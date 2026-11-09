using System.Diagnostics.CodeAnalysis;

using Datavanced.SmartTaskManagement.Domain.Primitives;

namespace Datavanced.SmartTaskManagement.Domain.Users;

/// <summary>Normalized (trimmed, lower-case) email address.</summary>
public sealed class Email : ValueObject
{
    public const int MaxLength = 256;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Email addresses are stored and displayed lower-case; this is display/storage normalization, not a comparison.")]
    public static Email Create(string value)
    {
        Guard.NotNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();
        Guard.MaxLength(normalized, MaxLength, nameof(value));

        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        var validShape = at > 0
            && at == normalized.LastIndexOf('@')
            && normalized.IndexOf('.', at + 1) > at + 1
            && !normalized.EndsWith('.');

        if (!validShape)
        {
            throw new ArgumentException("Must be a valid email address.", nameof(value));
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
