using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RefreshSession;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

public sealed class RefreshSessionCommandHandlerTests
{
    private static readonly string StoredHash = new('b', RefreshToken.TokenHashLength);

    private readonly AuthTestSupport _auth = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CapturingLogger _logger = new();

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
        added.FamilyId.ShouldBe(old.FamilyId);
        added.Id.ShouldNotBe(old.FamilyId);
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
        await _auth.RefreshTokens.DidNotReceive().RevokeActiveInFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_RotatedExactlyThirtySecondsAgo_IsReuseNotSuperseded()
    {
        var user = ExistingUser();
        var token = StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(30), replacedById: Guid.NewGuid());

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(token);
    }

    [Fact]
    public async Task Refresh_RotatedThirtyOneSecondsAgo_RevokesItsFamilyOnlyAndLogsWarning()
    {
        var user = ExistingUser();
        var token = StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(31), replacedById: Guid.NewGuid());
        _auth.RefreshTokens.RevokeActiveInFamilyAsync(token.FamilyId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(token);
        await _auth.RefreshTokens.DidNotReceive().ListActiveForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        var warning = _logger.Entries.ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(token.FamilyId.ToString());
        warning.Message.ShouldNotContain("raw");
    }

    [Fact]
    public async Task Refresh_RevokedTokenOfDeadFamily_Returns401AndLogsNoWarning()
    {
        // Example: a token revoked by a role change; its family has no active tokens left.
        var user = ExistingUser();
        var token = StoredToken(user.Id, revokedAgo: TimeSpan.FromHours(1), replacedById: null);
        _auth.RefreshTokens.RevokeActiveInFamilyAsync(token.FamilyId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(token);
        _logger.Entries.ShouldNotContain(e => e.Level >= LogLevel.Warning);
        _logger.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task Refresh_RevokedByLogout_IsReuseNotGrace()
    {
        var user = ExistingUser();
        var token = StoredToken(user.Id, revokedAgo: TimeSpan.FromSeconds(1), replacedById: null);

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidRefreshToken);
        await AssertFamilyRevokedAsync(token);
    }

    [Fact]
    public async Task Refresh_ConcurrencyConflictOnSave_ReturnsRefreshSuperseded()
    {
        var user = ExistingUser();
        StoredToken(user.Id);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("concurrency.conflict", "stale")));

        var result = await CreateHandler().Handle(new RefreshSessionCommand("raw"), CancellationToken.None);

        // The other request won the rotation; its cookie must survive (the controller deletes it only on invalid).
        result.Error.ShouldBe(AuthErrors.RefreshSuperseded);
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
            _logger);

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

    private async Task AssertFamilyRevokedAsync(RefreshToken token)
    {
        await _auth.RefreshTokens.Received(1).RevokeActiveInFamilyAsync(token.FamilyId, _auth.Time.GetUtcNow(), Arg.Any<CancellationToken>());
        await _auth.RefreshTokens.Received(1).RevokeActiveInFamilyAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _auth.RefreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }
}

/// <summary>Records formatted log entries so tests can assert level and content.</summary>
internal sealed class CapturingLogger : ILogger<RefreshSessionCommandHandler>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
