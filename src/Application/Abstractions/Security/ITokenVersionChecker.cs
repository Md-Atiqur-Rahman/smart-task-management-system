namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>Outcome of comparing a token's version with the user's current version.</summary>
public enum TokenVersionCheck
{
    Valid,
    Revoked,
    Unavailable,
}

/// <summary>
/// Decides whether an access token is still current (ADR 0013). Fails closed:
/// when the shared store is unreachable the result is <see cref="TokenVersionCheck.Unavailable"/>.
/// </summary>
public interface ITokenVersionChecker
{
    Task<TokenVersionCheck> CheckAsync(Guid userId, int tokenVersion, CancellationToken ct);
}
