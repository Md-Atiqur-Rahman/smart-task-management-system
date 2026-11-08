using Contoso.TemplateApp.Application.Abstractions.Security;

using Microsoft.AspNetCore.Identity;

namespace Contoso.TemplateApp.Infrastructure.Security;

/// <summary>IPasswordHasher over ASP.NET Core Identity's PasswordHasher (PBKDF2, salted, versioned).</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    // Identity's hasher takes a user object but the default implementation does not use it.
    private static readonly object User = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(User, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(User, passwordHash, providedPassword)
            != PasswordVerificationResult.Failed;
}
