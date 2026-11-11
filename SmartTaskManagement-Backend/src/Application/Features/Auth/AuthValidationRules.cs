using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>Validation rules shared by the auth validators.</summary>
internal static class AuthValidationRules
{
    /// <summary>Same rule as <see cref="Email.Create"/>.</summary>
    public static bool IsValidEmail(string? value)
    {
        if (value is null)
        {
            return false;
        }

        try
        {
            Email.Create(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
