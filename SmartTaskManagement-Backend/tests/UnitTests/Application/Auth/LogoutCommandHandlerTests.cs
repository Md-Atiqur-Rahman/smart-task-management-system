using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Logout;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

public sealed class LogoutCommandHandlerTests
{
    private static readonly string StoredHash = new('b', RefreshToken.TokenHashLength);

    private readonly AuthTestSupport _auth = new();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public LogoutCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _auth.Generator.Hash(Arg.Any<string>()).Returns(StoredHash);
    }

    [Fact]
    public async Task Logout_ActiveToken_RevokesOnlyThatToken()
    {
        var user = User.Register(Email.Create("a@x.com"), "Alice", "stored-hash");
        var versionBefore = user.TokenVersion;
        var token = RefreshToken.Issue(user.Id, StoredHash, _auth.Time.GetUtcNow(), TimeSpan.FromDays(7));
        _auth.RefreshTokens.GetByHashAsync(StoredHash, Arg.Any<CancellationToken>()).Returns(token);

        var result = await CreateHandler().Handle(new LogoutCommand("raw"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        token.RevokedAt.ShouldBe(_auth.Time.GetUtcNow());
        token.ReplacedById.ShouldBeNull();
        user.TokenVersion.ShouldBe(versionBefore);
        await _auth.RefreshTokens.DidNotReceive().ListActiveForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_NoTokenUnknownOrAlreadyRevoked_SucceedsWithoutChanges()
    {
        var revoked = RefreshToken.Issue(Guid.NewGuid(), StoredHash, _auth.Time.GetUtcNow(), TimeSpan.FromDays(7));
        revoked.Revoke(_auth.Time.GetUtcNow(), replacedById: null);

        (await CreateHandler().Handle(new LogoutCommand(null), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await CreateHandler().Handle(new LogoutCommand(string.Empty), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await CreateHandler().Handle(new LogoutCommand("unknown"), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        _auth.RefreshTokens.GetByHashAsync(StoredHash, Arg.Any<CancellationToken>()).Returns(revoked);
        (await CreateHandler().Handle(new LogoutCommand("raw"), CancellationToken.None)).IsSuccess.ShouldBeTrue();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auth.RefreshTokens.DidNotReceive().ListActiveForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_ConcurrencyConflictOnSave_StillSucceeds()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), StoredHash, _auth.Time.GetUtcNow(), TimeSpan.FromDays(7));
        _auth.RefreshTokens.GetByHashAsync(StoredHash, Arg.Any<CancellationToken>()).Returns(token);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("concurrency.conflict", "stale")));

        var result = await CreateHandler().Handle(new LogoutCommand("raw"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    private LogoutCommandHandler CreateHandler() => new(_auth.RefreshTokens, _auth.Generator, _unitOfWork, _auth.Time);
}
