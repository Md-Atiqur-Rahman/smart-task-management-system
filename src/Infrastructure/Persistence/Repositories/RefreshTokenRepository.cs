using Datavanced.SmartTaskManagement.Domain.Auth;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Set<RefreshToken>().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => context.Set<RefreshToken>().Add(token);
}
