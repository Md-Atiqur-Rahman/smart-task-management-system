using Datavanced.SmartTaskManagement.Domain.Auth;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain.Auth;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private static RefreshToken NewToken() =>
        RefreshToken.Issue(Guid.NewGuid(), new string('a', RefreshToken.TokenHashLength), Now, TimeSpan.FromDays(7));

    [Fact]
    public void Issue_ValidInput_IsActiveUntilExpiry()
    {
        var token = NewToken();

        token.ExpiresAt.ShouldBe(Now.AddDays(7));
        token.IsActive(Now.AddDays(7).AddTicks(-1)).ShouldBeTrue();
        token.IsActive(Now.AddDays(7)).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_Twice_SecondFailsWithInvalidRefreshToken()
    {
        var token = NewToken();

        token.Revoke(Now, null).IsSuccess.ShouldBeTrue();
        token.Revoke(Now, null).Error.ShouldBe(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public void IsWithinRotationGrace_RotatedTwentyNineSecondsAgo_IsTrue()
    {
        var token = NewToken();
        token.Revoke(Now, Guid.NewGuid());

        token.IsWithinRotationGrace(Now.AddSeconds(29), TimeSpan.FromSeconds(30)).ShouldBeTrue();
        token.IsWithinRotationGrace(Now.AddSeconds(30), TimeSpan.FromSeconds(30)).ShouldBeFalse();
    }

    [Fact]
    public void IsWithinRotationGrace_RevokedByLogout_IsFalse()
    {
        var token = NewToken();
        token.Revoke(Now, null);

        token.IsWithinRotationGrace(Now.AddSeconds(1), TimeSpan.FromSeconds(30)).ShouldBeFalse();
    }

    [Fact]
    public void Issue_HashOfWrongLength_Throws() =>
        Should.Throw<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), "short", Now, TimeSpan.FromDays(7)));
}
