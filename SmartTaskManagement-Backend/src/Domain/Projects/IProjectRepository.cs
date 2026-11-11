namespace Datavanced.SmartTaskManagement.Domain.Projects;

/// <summary>Write repository of the Project aggregate. Changes are saved by IUnitOfWork.</summary>
public interface IProjectRepository
{
    /// <summary>Tracked aggregate, or null when it does not exist or is soft-deleted.</summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when the user owns at least one project that is not soft-deleted.</summary>
    Task<bool> AnyOwnedByAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Project project);
}
