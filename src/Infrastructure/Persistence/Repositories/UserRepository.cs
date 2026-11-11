using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<User>().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Domain.Users.Email email, CancellationToken cancellationToken) =>
        context.Set<User>().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    // Soft-deleted users still hold their email (unique index), so the filter is ignored here.
    public Task<bool> EmailExistsAsync(Domain.Users.Email email, CancellationToken cancellationToken) =>
        context.Set<User>().IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken);

    public Task<int> CountByRoleAsync(Role role, CancellationToken cancellationToken) =>
        context.Set<User>().CountAsync(u => u.Role == role, cancellationToken);

    public void Add(User user) => context.Set<User>().Add(user);
}
