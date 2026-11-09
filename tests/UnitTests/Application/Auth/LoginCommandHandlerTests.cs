using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Login;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

public sealed class LoginCommandHandlerTests
{
    private readonly AuthTestSupport _auth = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public LoginCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _hasher.Hash(Arg.Any<string>()).Returns("dummy-hash");
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentials()
    {
        ExistingUser();
        _hasher.Verify("stored-hash", "wrong").Returns(false);

        var result = await CreateHandler().Handle(new LoginCommand("a@x.com", "wrong"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsInvalidCredentialsAndStillVerifiesAHash()
    {
        var result = await CreateHandler().Handle(new LoginCommand("nobody@x.com", "whatever"), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.InvalidCredentials);
        _hasher.Received(1).Verify(Arg.Any<string>(), "whatever");
    }

    [Fact]
    public async Task Login_Valid_ReturnsTokensWithCurrentVersionAndPublishesIt()
    {
        var user = ExistingUser(roleChanges: 3);
        _hasher.Verify("stored-hash", "Passw0rdOk").Returns(true);

        var result = await CreateHandler().Handle(new LoginCommand("A@x.com", "Passw0rdOk"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.User.Id.ShouldBe(user.Id);
        user.TokenVersion.ShouldBe(4);
        _auth.TokenService.Received(1).CreateAccessToken(user.Id, user.Role, 4);
        _auth.RefreshTokens.Received(1).Add(Arg.Any<RefreshToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auth.VersionStore.Received(1).SetIfHigherAsync(user.Id, 4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_RedisDown_StillSucceeds()
    {
        ExistingUser();
        _hasher.Verify("stored-hash", "Passw0rdOk").Returns(true);
        _auth.VersionStore.SetIfHigherAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SharedStateUnavailableException());

        var result = await CreateHandler().Handle(new LoginCommand("a@x.com", "Passw0rdOk"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    private LoginCommandHandler CreateHandler() =>
        new(_users, _unitOfWork, _hasher, _auth.Issuer);

    private User ExistingUser(int roleChanges = 0)
    {
        var user = User.Register(Email.Create("a@x.com"), "Alice", "stored-hash");
        for (var i = 0; i < roleChanges; i++)
        {
            user.ChangeRole(i % 2 == 0 ? Role.Admin : Role.TeamMember);
        }

        _users.GetByEmailAsync(Email.Create("a@x.com"), Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }
}
