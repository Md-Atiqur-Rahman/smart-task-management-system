using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Users;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>AsNoTracking + Select projections for the user read side.</summary>
public sealed class UserReadRepository(AppDbContext context) : IUserReadRepository
{
    public Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken ct) =>
        context.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (int?)u.TokenVersion)
            .SingleOrDefaultAsync(ct);

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid id, CancellationToken ct)
    {
        // Email is a value object stored through a converter; project it, then read Value in memory.
        var row = await context.Set<User>()
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.FullName, u.Email, u.Role })
            .SingleOrDefaultAsync(ct);

        return row is null ? null : new CurrentUserDto(row.Id, row.FullName, row.Email.Value, row.Role);
    }
}
