using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RegisterUser;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void RegisterValidator_ValidCommand_IsValid() =>
        _validator.Validate(new RegisterUserCommand("a@x.com", "Alice", "Passw0rdOk")).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("short1")]
    [InlineData("allletters")]
    [InlineData("12345678")]
    public void RegisterValidator_WeakPassword_HasPasswordError(string password)
    {
        var result = _validator.Validate(new RegisterUserCommand("a@x.com", "Alice", password));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterUserCommand.Password));
    }

    [Fact]
    public void RegisterValidator_Password129Chars_HasPasswordError()
    {
        var password = new string('a', PasswordRules.MaxLength) + "1";

        var result = _validator.Validate(new RegisterUserCommand("a@x.com", "Alice", password));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterUserCommand.Password));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void RegisterValidator_BadEmail_HasEmailError(string email)
    {
        var result = _validator.Validate(new RegisterUserCommand(email, "Alice", "Passw0rdOk"));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterUserCommand.Email));
    }
}
