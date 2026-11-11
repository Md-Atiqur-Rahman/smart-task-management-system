using Datavanced.SmartTaskManagement.Domain.Primitives;

namespace Datavanced.SmartTaskManagement.Domain.Projects;

/// <summary>Project aggregate: a named piece of work owned by one Project Manager (referenced by id).</summary>
public sealed class Project : AuditableAggregateRoot<Guid>
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    // Used by Create and by EF Core materialization (constructor binding by name).
    private Project(Guid id, string name, string? description, Guid ownerId)
        : base(id)
    {
        Name = name;
        Description = description;
        OwnerId = ownerId;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid OwnerId { get; private set; }

    public static Project Create(string name, string? description, Guid ownerId)
    {
        var trimmedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);
        GuardOwner(ownerId);

        return new Project(Guid.CreateVersion7(), trimmedName, normalizedDescription, ownerId);
    }

    public void UpdateDetails(string name, string? description)
    {
        var trimmedName = NormalizeName(name);
        var normalizedDescription = NormalizeDescription(description);

        Name = trimmedName;
        Description = normalizedDescription;
    }

    /// <summary>Returns true when the owner changed.</summary>
    public bool ChangeOwner(Guid newOwnerId)
    {
        GuardOwner(newOwnerId);

        if (OwnerId == newOwnerId)
        {
            return false;
        }

        OwnerId = newOwnerId;
        return true;
    }

    public void Delete(DateTimeOffset deletedAt) => MarkDeleted(deletedAt);

    private static string NormalizeName(string name)
    {
        Guard.NotNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        Guard.MaxLength(trimmed, NameMaxLength, nameof(name));
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        Guard.MaxLength(trimmed, DescriptionMaxLength, nameof(description));
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void GuardOwner(Guid ownerId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("The owner id must not be empty.", nameof(ownerId));
        }
    }
}
