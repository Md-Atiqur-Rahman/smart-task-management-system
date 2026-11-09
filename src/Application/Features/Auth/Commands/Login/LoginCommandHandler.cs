using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Login;

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    AuthSessionIssuer sessionIssuer,
    DummyPasswordHash dummyPasswordHash)
    : ICommandHandler<LoginCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(Email.Create(command.Email), cancellationToken);
        if (user is null)
        {
            passwordHasher.Verify(dummyPasswordHash.Value, command.Password);
            return Result.Failure<AuthTokens>(AuthErrors.InvalidCredentials);
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            return Result.Failure<AuthTokens>(AuthErrors.InvalidCredentials);
        }

        var tokens = sessionIssuer.Issue(user);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<AuthTokens>(saved.Error);
        }

        await sessionIssuer.PublishTokenVersionAsync(user, cancellationToken);
        return Result.Success(tokens);
    }
}
