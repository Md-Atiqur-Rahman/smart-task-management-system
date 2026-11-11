using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Seeding;

/// <summary>All empty is valid (skip); a partial set or a policy-breaking value fails startup. Never echoes the password.</summary>
public sealed class AdminSeedOptionsValidator : IValidateOptions<AdminSeedOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminSeedOptions options)
    {
        if (options.IsEmpty)
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Fail(
                "AdminSeed:Email, AdminSeed:Password and AdminSeed:FullName must be all set or all empty.");
        }

        var failures = new List<string>();
        if (!IsValidEmail(options.Email!))
        {
            failures.Add("AdminSeed:Email must be a valid email address.");
        }

        if (options.FullName!.Trim().Length > User.FullNameMaxLength)
        {
            failures.Add($"AdminSeed:FullName must be at most {User.FullNameMaxLength} characters.");
        }

        if (!PasswordRules.IsValid(options.Password!))
        {
            failures.Add(
                $"AdminSeed:Password must be {PasswordRules.MinLength} to {PasswordRules.MaxLength} characters "
                + "and contain at least one letter and one digit.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            Domain.Users.Email.Create(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
