using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RegisterUser;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Auth;

public sealed class RegisterUserCommandHandlerTests
{
    private readonly AuthTestSupport _auth = new();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public RegisterUserCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _hasher.Hash(Arg.Any<string>()).Returns("password-hash");
    }

    [Fact]
    public async Task Register_NewEmail_AddsTeamMemberSavesAndReturnsTokens()
    {
        var result = await CreateHandler().Handle(
            new RegisterUserCommand("A@X.com", "Alice", "Passw0rdOk"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.User.Email.ShouldBe("a@x.com");
        result.Value.RefreshToken.ShouldBe("raw-token");
        _users.Received(1).Add(Arg.Is<User>(u => u.Role == Role.TeamMember && u.PasswordHash == "password-hash"));
        _auth.RefreshTokens.Received(1).Add(Arg.Any<RefreshToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auth.VersionStore.Received(1).SetIfHigherAsync(result.Value.User.Id, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_EmailExistsInOtherCase_ReturnsEmailTaken()
    {
        _users.EmailExistsAsync(Email.Create("A@X.com"), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(
            new RegisterUserCommand("a@x.com", "Alice", "Passw0rdOk"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.EmailTaken);
        _users.DidNotReceive().Add(Arg.Any<User>());
        _auth.RefreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_DuplicateKeyOnSave_ReturnsEmailTaken()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(PersistenceErrors.DuplicateKey));

        var result = await CreateHandler().Handle(
            new RegisterUserCommand("a@x.com", "Alice", "Passw0rdOk"), CancellationToken.None);

        result.Error.ShouldBe(UserErrors.EmailTaken);
        await _auth.VersionStore.DidNotReceive().SetIfHigherAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private RegisterUserCommandHandler CreateHandler() =>
        new(_users, _unitOfWork, _hasher, _auth.Issuer);
}
