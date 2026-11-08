// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Domain.Primitives;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Samples;

/// <summary>
/// Reference aggregate: factory with guard clauses, private setters, a state
/// change that returns Result, and soft delete.
/// </summary>
public sealed class Sample : AuditableAggregateRoot<Guid>
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;

    // Used by Create and by EF Core materialization (constructor binding by name).
    private Sample(Guid id, string name, string? description)
        : base(id)
    {
        Name = name;
        Description = description;
        Status = SampleStatus.Open;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public SampleStatus Status { get; private set; }

    public static Sample Create(string name, string? description)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.MaxLength(name, NameMaxLength);
        Guard.MaxLength(description, DescriptionMaxLength);

        return new Sample(Guid.CreateVersion7(), name, description);
    }

    /// <summary>Open -> Completed. Completing twice is an expected failure (Conflict).</summary>
    public Result Complete()
    {
        if (Status == SampleStatus.Completed)
        {
            return Result.Failure(SampleErrors.AlreadyCompleted);
        }

        Status = SampleStatus.Completed;
        return Result.Success();
    }

    public void Delete(DateTimeOffset deletedAt) => MarkDeleted(deletedAt);
}
