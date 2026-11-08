using System.Runtime.CompilerServices;

namespace Datavanced.SmartTaskManagement.Domain.Primitives;

/// <summary>
/// Guard clauses for programmer errors and invariants. They always throw.
/// Expected, user-caused failures return a Result instead.
/// </summary>
public static class Guard
{
    public static string NotNullOrWhiteSpace(
        string value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    public static string? MaxLength(
        string? value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Must be at most {maxLength} characters.", parameterName);
        }

        return value;
    }
}
