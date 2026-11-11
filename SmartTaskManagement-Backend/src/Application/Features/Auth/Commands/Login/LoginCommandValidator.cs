using Datavanced.SmartTaskManagement.Domain.Users;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Login;

/// <summary>Login checks only that both values are present and well-formed; it gives no password policy hints.</summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Email.MaxLength).Must(AuthValidationRules.IsValidEmail)
            .WithMessage("'{PropertyName}' must be a valid email address.");
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}
