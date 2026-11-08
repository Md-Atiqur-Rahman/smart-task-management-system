using Contoso.TemplateApp.Domain.Primitives;

namespace Contoso.TemplateApp.UnitTests.Domain;

public sealed class GuardTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhiteSpace_Blank_ThrowsArgumentException(string value)
    {
        Should.Throw<ArgumentException>(() => Guard.NotNullOrWhiteSpace(value));
    }

    [Fact]
    public void NotNullOrWhiteSpace_Value_ReturnsIt()
    {
        Guard.NotNullOrWhiteSpace("name").ShouldBe("name");
    }

    [Fact]
    public void MaxLength_TooLong_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Guard.MaxLength("abcd", 3));
    }

    [Fact]
    public void MaxLength_NullableParameter_AcceptsNull()
    {
        // Proves: a `string?` parameter accepts null under the woven assembly
        // (NullGuard Mode=Implicit must honour the nullable annotation).
        Should.NotThrow(() => Guard.MaxLength(null, 3));
    }
}
