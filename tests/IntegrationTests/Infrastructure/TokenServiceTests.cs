using System.Text;

using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Security;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Access token writer and refresh token generator (no Docker needed).</summary>
public sealed class TokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = TestTokens.Issuer,
        Audience = TestTokens.Audience,
        SigningKey = TestTokens.SigningKey,
        AccessTokenMinutes = 10,
    };

    [Fact]
    public async Task CreateAccessToken_ValidUser_HasSubRoleTvJtiAndTenMinuteExpiry()
    {
        var time = new FakeTimeProvider(Now);
        var service = new JwtTokenService(Options.Create(_options), time);
        var userId = Guid.NewGuid();

        var token = service.CreateAccessToken(userId, Role.ProjectManager, 3);

        token.ExpiresAt.ShouldBe(Now.AddMinutes(10));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.Value,
            new TokenValidationParameters
            {
                ValidIssuer = TestTokens.Issuer,
                ValidAudience = TestTokens.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestTokens.SigningKey)),
                LifetimeValidator = (_, expires, _, _) => expires > time.GetUtcNow().UtcDateTime,
            });
        result.IsValid.ShouldBeTrue();
        var jwt = (JsonWebToken)result.SecurityToken;
        jwt.GetClaim("sub").Value.ShouldBe(userId.ToString());
        jwt.GetClaim("role").Value.ShouldBe("ProjectManager");
        jwt.GetClaim("tv").Value.ShouldBe("3");
        jwt.GetClaim("jti").Value.ShouldNotBeNullOrEmpty();
        new DateTimeOffset(jwt.ValidTo).ShouldBe(Now.AddMinutes(10));
    }

    [Fact]
    public void Generate_TwoCalls_DifferentRawTokensAnd64CharHashes()
    {
        var generator = new RefreshTokenGenerator();

        var first = generator.Generate();
        var second = generator.Generate();

        first.RawToken.ShouldNotBe(second.RawToken);
        first.TokenHash.Length.ShouldBe(64);
        second.TokenHash.Length.ShouldBe(64);
        generator.Hash(first.RawToken).ShouldBe(first.TokenHash);
    }

    [Fact]
    public void Hash_SameInput_SameOutput_AndGarbageInputDoesNotThrow()
    {
        var generator = new RefreshTokenGenerator();

        generator.Hash("abc").ShouldBe(generator.Hash("abc"));
        generator.Hash(new string('%', 5000)).Length.ShouldBe(64);
    }
}
