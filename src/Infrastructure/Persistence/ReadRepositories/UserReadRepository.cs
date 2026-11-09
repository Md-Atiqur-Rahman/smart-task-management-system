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
}
