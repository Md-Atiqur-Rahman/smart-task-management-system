using Datavanced.SmartTaskManagement.Application.Features.Users;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// Store hit: compare. Miss: load the version from the database through the read
/// repository, store it (forward-only) and compare. Unknown user: revoked.
/// </summary>
internal sealed class TokenVersionChecker(ITokenVersionStore store, IUserReadRepository users) : ITokenVersionChecker
{
    public async Task<TokenVersionCheck> CheckAsync(Guid userId, int tokenVersion, CancellationToken ct)
    {
        try
        {
            var current = await store.GetAsync(userId, ct);
            if (current is null)
            {
                current = await users.GetTokenVersionAsync(userId, ct);
                if (current is null)
                {
                    return TokenVersionCheck.Revoked;
                }

                await store.SetIfHigherAsync(userId, current.Value, ct);
            }

            return tokenVersion == current.Value ? TokenVersionCheck.Valid : TokenVersionCheck.Revoked;
        }
        catch (SharedStateUnavailableException)
        {
            return TokenVersionCheck.Unavailable;
        }
    }
}
