using Datavanced.SmartTaskManagement.Domain.Tasks;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

public sealed class TaskItemRepository(AppDbContext context) : ITaskItemRepository
{
    // The soft-delete query filter excludes deleted tasks.
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<TaskItem>().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Add(TaskItem task) => context.Set<TaskItem>().Add(task);
}
