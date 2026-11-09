using Datavanced.SmartTaskManagement.Domain.Users;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Email.MaxLength).Must(AuthValidationRules.IsValidEmail)
            .WithMessage("'{PropertyName}' must be a valid email address.");
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(User.FullNameMaxLength);
        RuleFor(c => c.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinLength)
            .MaximumLength(PasswordRules.MaxLength)
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("'{PropertyName}' must contain at least one letter and one digit.");
    }
}
