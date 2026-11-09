namespace Datavanced.SmartTaskManagement.Domain.Auth;

/// <summary>Write repository of the RefreshToken aggregate. Changes are saved by IUnitOfWork.</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Tracked aggregate, or null when no token has this hash.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Tracked tokens of the user that are not revoked and not expired at <paramref name="now"/>.</summary>
    Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}
