using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain;

/// <summary>
/// Proves that NullGuard actually wove the Domain assembly: Error has no
/// hand-written null checks, so an ArgumentNullException can only come from
/// the woven code.
/// </summary>
public sealed class NullGuardWeavingTests
{
    [Fact]
    public void NonNullableParameter_Null_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => Error.NotFound(null!, "description"));

        exception.ParamName.ShouldBe("code");
    }
}
