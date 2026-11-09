using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RefreshSession;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

public sealed class RefreshSessionCommandHandlerTests
{
    private static readonly string StoredHash = new('b', RefreshToken.TokenHashLength);

    private readonly AuthTestSupport _auth = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public RefreshSessionCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _auth.Generator.Hash(Arg.Any<string>()).Returns(StoredHash);
    }

    [Fact]
    public async Task Refresh_ActiveToken_RevokesItWithReplacementAndIssuesNewPair()
    {
        var user = ExistingUser();
        var old = StoredToken(user.Id);
        RefreshToken? added = null;
        _auth.RefreshTokens.When(r => r.Add(Arg.Any<RefreshToken>())).Do(c => added = c.Arg<RefreshToken>());

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        old.RevokedAt.ShouldBe(_auth.Time.GetUtcNow());
        old.ReplacedById.ShouldBe(added.Id);
        _auth.TokenService.Received(1).CreateAccessToken(user.Id, user.Role, user.TokenVersion);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auth.VersionStore.Received(1).SetIfHigherAsync(user.Id, user.TokenVersion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_UnknownOrGarbageToken_ReturnsInvalidRefreshToken()
    {
        var result = await CreateHandler().Handle(new RefreshSessionCommand(new string('%', 5000)), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ReturnsInvalidRefreshToken()
    {
        var user = ExistingUser();
        StoredToken(user.Id, issuedAgo: TimeSpan.FromDays(8));

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_RotatedTenSecondsAgo_ReturnsSupersededAndRevokesNothing()
    {
        var user = ExistingUser();
        StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(10), replacedById: Guid.NewGuid());

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.RefreshSuperseded);
        await _auth.RefreshTokens.DidNotReceive().RevokeAllActiveForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_RotatedExactlyThirtySecondsAgo_IsReuseNotSuperseded()
    {
        var user = ExistingUser();
        StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(30), replacedById: Guid.NewGuid());

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(user.Id);
    }

    [Fact]
    public async Task Refresh_RotatedThirtyOneSecondsAgo_RevokesAllActiveTokensOfUser()
    {
        var user = ExistingUser();
        StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(31), replacedById: Guid.NewGuid());

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(user.Id);
    }

    [Fact]
    public async Task Refresh_RevokedByLogout_IsReuseNotGrace()
    {
        var user = ExistingUser();
        StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(1), replacedById: null);

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(user.Id);
    }

    [Fact]
    public async Task Refresh_ConcurrencyConflictOnSave_ReturnsInvalidRefreshToken()
    {
        var user = ExistingUser();
        StoredToken(user.Id);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("concurrency.conflict", "stale")));

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await _auth.VersionStore.DidNotReceive().SetIfHigherAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private RefreshSessionCommandHandler CreateHandler() =>
        new(
            _auth.RefreshTokens,
            _auth.Generator,
            _users,
            _unitOfWork,
            _auth.Issuer,
            _auth.Time,
            Options.Create(new RefreshTokenOptions()),
            NullLogger<RefreshSessionCommandHandler>.Instance);

    private User ExistingUser()
    {
        var user = User.Register(Email.Create("a@x.com"), "Alice", "stored-hash");
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private RefreshToken StoredToken(Guid userId, TimeSpan? issuedAgo = null, TimeSpan? revokedAgo = null, Guid? replacedById = null)
    {
        var now = _auth.Time.GetUtcNow();
        var token = RefreshToken.Issue(userId, StoredHash, now - (issuedAgo ?? TimeSpan.FromMinutes(5)), TimeSpan.FromDays(7));
        if (revokedAgo is { } ago)
        {
            token.Revoke(now - ago, replacedById);
        }

        _auth.RefreshTokens.GetByHashAsync(StoredHash, Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    private async Task AssertFamilyRevokedAsync(Guid userId)
    {
        await _auth.RefreshTokens.Received(1).RevokeAllActiveForUserAsync(userId, _auth.Time.GetUtcNow(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _auth.RefreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }
}
