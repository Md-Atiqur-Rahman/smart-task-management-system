namespace Contoso.TemplateApp.Application.Abstractions.Security;

/// <summary>Password hashing abstraction. Implemented in Infrastructure with ASP.NET Core Identity's PasswordHasher.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string providedPassword);
}
