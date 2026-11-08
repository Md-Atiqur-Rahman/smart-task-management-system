using Contoso.TemplateApp.Infrastructure.Security;

namespace Contoso.TemplateApp.IntegrationTests.Infrastructure;

/// <summary>IPasswordHasher adapter over ASP.NET Core Identity's PasswordHasher.</summary>
public sealed class PasswordHasherTests
{
    private readonly PasswordHasherAdapter _hasher = new();

    [Fact]
    public void Hash_ThenVerify_RoundTrips()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        hash.ShouldNotBe("correct horse battery staple");
        _hasher.Verify(hash, "correct horse battery staple").ShouldBeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify(hash, "wrong").ShouldBeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_UsesDifferentSalts()
    {
        _hasher.Hash("same").ShouldNotBe(_hasher.Hash("same"));
    }
}
