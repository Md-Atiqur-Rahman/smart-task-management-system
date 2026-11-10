using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain.Users;

public sealed class EmailTests
{
    [Fact]
    public void Create_MixedCaseWithSpaces_IsTrimmedAndLowerCased()
    {
        Email.Create("  Rahim@Example.COM ").Value.ShouldBe("rahim@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("@x.com")]
    [InlineData("a@@x.com")]
    public void Create_InvalidFormat_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => Email.Create(value));
    }

    [Fact]
    public void Create_LongerThanMax_Throws()
    {
        Should.Throw<ArgumentException>(() => Email.Create(new string('a', 251) + "@x.com"));
    }

    [Fact]
    public void Equality_SameNormalizedValue_IsEqual()
    {
        Email.Create("A@X.com").ShouldBe(Email.Create("a@x.com"));
    }
}
