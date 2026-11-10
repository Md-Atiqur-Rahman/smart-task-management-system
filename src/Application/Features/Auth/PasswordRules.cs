namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>Password policy limits shared by the validators.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    public const int MaxLength = 128;

    /// <summary>True when the password meets the full policy (length, at least one letter and one digit).</summary>
    public static bool IsValid(string password) =>
        password.Length is >= MinLength and <= MaxLength && HasLetterAndDigit(password);

    public static bool HasLetterAndDigit(string password) =>
        password.Any(char.IsLetter) && password.Any(char.IsDigit);
}
