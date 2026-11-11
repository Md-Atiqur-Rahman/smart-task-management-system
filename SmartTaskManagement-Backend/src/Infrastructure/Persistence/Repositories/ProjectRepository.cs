using Datavanced.SmartTaskManagement.Domain.Projects;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(AppDbContext context) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<Project>().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    // The soft-delete query filter excludes deleted projects.
    public Task<bool> AnyOwnedByAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Set<Project>().AnyAsync(p => p.OwnerId == userId, cancellationToken);

    public void Add(Project project) => context.Set<Project>().Add(project);
}
