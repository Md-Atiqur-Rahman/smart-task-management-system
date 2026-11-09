using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

/// <summary>Shared substitutes and the real AuthSessionIssuer wired to them.</summary>
internal sealed class AuthTestSupport
{
    public AuthTestSupport()
    {
        Generator.Generate().Returns(new GeneratedRefreshToken("raw-token", new string('a', RefreshToken.TokenHashLength)));
        TokenService.CreateAccessToken(Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<int>())
            .Returns(new AccessToken("access", Time.GetUtcNow().AddMinutes(15)));
        Issuer = new AuthSessionIssuer(
            RefreshTokens,
            Generator,
            TokenService,
            VersionStore,
            Time,
            Options.Create(new RefreshTokenOptions()),
            NullLogger<AuthSessionIssuer>.Instance);
    }

    public IRefreshTokenRepository RefreshTokens { get; } = Substitute.For<IRefreshTokenRepository>();

    public IRefreshTokenGenerator Generator { get; } = Substitute.For<IRefreshTokenGenerator>();

    public ITokenService TokenService { get; } = Substitute.For<ITokenService>();

    public ITokenVersionStore VersionStore { get; } = Substitute.For<ITokenVersionStore>();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    public AuthSessionIssuer Issuer { get; }
}
