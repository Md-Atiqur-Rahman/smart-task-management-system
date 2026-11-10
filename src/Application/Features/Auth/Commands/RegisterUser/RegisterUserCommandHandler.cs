using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RegisterUser;

internal sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    AuthSessionIssuer sessionIssuer)
    : ICommandHandler<RegisterUserCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = Email.Create(command.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<AuthTokens>(UserErrors.EmailTaken);
        }

        var user = User.Register(email, command.FullName, passwordHasher.Hash(command.Password));
        users.Add(user);
        var tokens = sessionIssuer.Issue(user);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            // A concurrent registration with the same email hits the unique index on Users.Email.
            return Result.Failure<AuthTokens>(
                saved.Error.Code == PersistenceErrors.DuplicateKeyCode ? UserErrors.EmailTaken : saved.Error);
        }

        await sessionIssuer.PublishTokenVersionAsync(user, cancellationToken);
        return Result.Success(tokens);
    }
}
