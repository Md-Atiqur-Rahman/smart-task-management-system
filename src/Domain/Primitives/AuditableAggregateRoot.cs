using System.Diagnostics.CodeAnalysis;

namespace Contoso.TemplateApp.Domain.Primitives;

/// <summary>
/// Aggregate root with audit fields, soft delete and optimistic concurrency.
/// CreatedAt and UpdatedAt are set by Infrastructure from TimeProvider;
/// RowVersion is set by the database.
/// </summary>
public abstract class AuditableAggregateRoot<TId> : AggregateRoot<TId>
    where TId : notnull
{
    protected AuditableAggregateRoot(TId id)
        : base(id)
    {
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "EF Core maps the SQL Server rowversion concurrency token to byte[].")]
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Soft delete. The global query filter hides the aggregate afterwards.</summary>
    protected void MarkDeleted(DateTimeOffset deletedAt)
    {
        IsDeleted = true;
        DeletedAt = deletedAt;
    }
}
