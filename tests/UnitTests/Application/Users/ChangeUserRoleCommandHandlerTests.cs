using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Users.Commands.ChangeUserRole;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Users;

public sealed class ChangeUserRoleCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenVersionStore _versions = Substitute.For<ITokenVersionStore>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    public ChangeUserRoleCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _refreshTokens.ListActiveForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    [Fact]
    public async Task ChangeRole_Valid_WritesRedisThenRevokesTokensAndSavesOnce()
    {
        var user = User.Register(Email.Create("a@x.com"), "Alice", "hash");
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var token = RefreshToken.Issue(user.Id, new string('c', RefreshToken.TokenHashLength), _time.GetUtcNow(), TimeSpan.FromDays(7));
        _refreshTokens.ListActiveForUserAsync(user.Id, _time.GetUtcNow(), Arg.Any<CancellationToken>()).Returns([token]);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand(user.Id, Role.ProjectManager), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        user.Role.ShouldBe(Role.ProjectManager);
        token.RevokedAt.ShouldBe(_time.GetUtcNow());
        Received.InOrder(() =>
        {
            _versions.SetIfHigherAsync(user.Id, 2, Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRole_RedisUnavailable_Returns503AndSavesNothing()
    {
        var user = User.Register(Email.Create("a@x.com"), "Alice", "hash");
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _versions.SetIfHigherAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SharedStateUnavailableException());

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand(user.Id, Role.ProjectManager), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.Unavailable);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRole_SameRole_SucceedsWithoutRedisOrSave()
    {
        var user = User.Register(Email.Create("a@x.com"), "Alice", "hash");
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand(user.Id, Role.TeamMember), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _versions.DidNotReceive().SetIfHigherAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRole_LastAdminDemoted_ReturnsLastAdmin()
    {
        var admin = User.CreateSeedAdmin(Email.Create("admin@x.com"), "Root", "hash");
        _users.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        _users.CountByRoleAsync(Role.Admin, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand(admin.Id, Role.TeamMember), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.LastAdmin);
        admin.Role.ShouldBe(Role.Admin);
        await _versions.DidNotReceive().SetIfHigherAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeRole_UnknownUser_ReturnsNotFound()
    {
        var result = await CreateHandler().Handle(
            new ChangeUserRoleCommand(Guid.NewGuid(), Role.Admin), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.NotFound);
    }

    private ChangeUserRoleCommandHandler CreateHandler() => new(_users, _refreshTokens, _versions, _unitOfWork, _time);
}
