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
    AuthSessionIssuer sessionIssuer)
    : ICommandHandler<LoginCommand, AuthTokens>
{
    // Hash of a random throwaway password, created once per process on first use (by the hasher
    // of the first request). An unknown email then costs one hash verification, like a wrong
    // password, so response time does not reveal which emails exist. Benign race: every value is equivalent.
    private static string? s_dummyHash;

    public async Task<Result<AuthTokens>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(Email.Create(command.Email), cancellationToken);
        if (user is null)
        {
            passwordHasher.Verify(GetDummyHash(), command.Password);
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

    private string GetDummyHash()
    {
        var existing = Volatile.Read(ref s_dummyHash);
        if (existing is not null)
        {
            return existing;
        }

        var created = passwordHasher.Hash(Guid.NewGuid().ToString("N"));
        return Interlocked.CompareExchange(ref s_dummyHash, created, null) ?? created;
    }
}
