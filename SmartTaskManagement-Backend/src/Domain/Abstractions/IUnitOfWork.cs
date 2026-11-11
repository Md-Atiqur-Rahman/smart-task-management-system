using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Abstractions;

/// <summary>Commits all changes of one use case atomically.</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves changes. Returns a Conflict error when another request changed the
    /// same aggregate first (optimistic concurrency).
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default);
}
