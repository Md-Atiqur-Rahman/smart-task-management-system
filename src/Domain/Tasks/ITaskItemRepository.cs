namespace Datavanced.SmartTaskManagement.Domain.Tasks;

/// <summary>Write repository of the TaskItem aggregate. Changes are saved by IUnitOfWork.</summary>
public interface ITaskItemRepository
{
    /// <summary>Tracked aggregate, or null when it does not exist or is soft-deleted.</summary>
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(TaskItem task);
}
