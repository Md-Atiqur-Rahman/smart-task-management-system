using Datavanced.SmartTaskManagement.Domain.Primitives;

namespace Datavanced.SmartTaskManagement.Domain.Tasks;

/// <summary>TaskItem aggregate: a unit of work in a project, optionally assigned to a user (both referenced by id).</summary>
public sealed class TaskItem : AuditableAggregateRoot<Guid>
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    // Used by Create and by EF Core materialization (constructor binding by name).
    private TaskItem(
        Guid id,
        Guid projectId,
        string title,
        string? description,
        TaskItemStatus status,
        TaskPriority priority,
        DateOnly? dueDate,
        Guid? assigneeId)
        : base(id)
    {
        ProjectId = projectId;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        DueDate = dueDate;
        AssigneeId = assigneeId;
    }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public static TaskItem Create(
        Guid projectId,
        string title,
        string? description,
        TaskPriority priority,
        DateOnly? dueDate,
        Guid? assigneeId)
    {
        var trimmedTitle = NormalizeTitle(title);
        var normalizedDescription = NormalizeDescription(description);
        GuardProject(projectId);
        GuardAssignee(assigneeId);

        return new TaskItem(
            Guid.CreateVersion7(),
            projectId,
            trimmedTitle,
            normalizedDescription,
            TaskItemStatus.ToDo,
            priority,
            dueDate,
            assigneeId);
    }

    public void UpdateDetails(string title, string? description, TaskPriority priority, DateOnly? dueDate)
    {
        var trimmedTitle = NormalizeTitle(title);
        var normalizedDescription = NormalizeDescription(description);

        Title = trimmedTitle;
        Description = normalizedDescription;
        Priority = priority;
        DueDate = dueDate;
    }

    /// <summary>Any status may change to any other. Returns true when the status changed.</summary>
    public bool ChangeStatus(TaskItemStatus newStatus)
    {
        if (Status == newStatus)
        {
            return false;
        }

        Status = newStatus;
        return true;
    }

    /// <summary>Null unassigns. Returns true when the assignee changed.</summary>
    public bool Assign(Guid? assigneeId)
    {
        GuardAssignee(assigneeId);

        if (AssigneeId == assigneeId)
        {
            return false;
        }

        AssigneeId = assigneeId;
        return true;
    }

    public void Delete(DateTimeOffset deletedAt) => MarkDeleted(deletedAt);

    private static string NormalizeTitle(string title)
    {
        Guard.NotNullOrWhiteSpace(title);

        var trimmed = title.Trim();
        Guard.MaxLength(trimmed, TitleMaxLength, nameof(title));
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        Guard.MaxLength(trimmed, DescriptionMaxLength, nameof(description));
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void GuardProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("The project id must not be empty.", nameof(projectId));
        }
    }

    private static void GuardAssignee(Guid? assigneeId)
    {
        if (assigneeId == Guid.Empty)
        {
            throw new ArgumentException("The assignee id must not be empty.", nameof(assigneeId));
        }
    }
}
