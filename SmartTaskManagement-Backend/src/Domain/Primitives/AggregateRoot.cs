namespace Datavanced.SmartTaskManagement.Domain.Primitives;

/// <summary>
/// Consistency boundary. Has exactly one write repository. Other aggregates
/// reference it only by Id.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id)
        : base(id)
    {
    }
}
