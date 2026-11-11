using Datavanced.SmartTaskManagement.Domain.Primitives;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Domain.Notifications;

/// <summary>
/// Notification aggregate: an in-app message for one recipient plus its email delivery state.
/// Other aggregates are referenced by id only. No soft delete: old rows are purged.
/// </summary>
public sealed class Notification : AggregateRoot<Guid>
{
    public const int SubjectMaxLength = 200;
    public const int ActorNameMaxLength = User.FullNameMaxLength;
    public const int EmailErrorMaxLength = 1000;

    // Used by Create and by EF Core materialization (constructor binding by name).
    private Notification(
        Guid id,
        Guid recipientId,
        NotificationType type,
        Guid? taskId,
        Guid? projectId,
        string subject,
        string actorName,
        DateTimeOffset createdAt,
        DateTimeOffset? readAt,
        EmailDeliveryStatus emailStatus,
        int emailAttempts,
        DateTimeOffset emailNextAttemptAt,
        DateTimeOffset? emailClaimedUntil,
        DateTimeOffset? emailSentAt,
        string? emailLastError)
        : base(id)
    {
        RecipientId = recipientId;
        Type = type;
        TaskId = taskId;
        ProjectId = projectId;
        Subject = subject;
        ActorName = actorName;
        CreatedAt = createdAt;
        ReadAt = readAt;
        EmailStatus = emailStatus;
        EmailAttempts = emailAttempts;
        EmailNextAttemptAt = emailNextAttemptAt;
        EmailClaimedUntil = emailClaimedUntil;
        EmailSentAt = emailSentAt;
        EmailLastError = emailLastError;
    }

    public Guid RecipientId { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid? TaskId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string Subject { get; private set; }

    public string ActorName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public EmailDeliveryStatus EmailStatus { get; private set; }

    public int EmailAttempts { get; private set; }

    public DateTimeOffset EmailNextAttemptAt { get; private set; }

    public DateTimeOffset? EmailClaimedUntil { get; private set; }

    public DateTimeOffset? EmailSentAt { get; private set; }

    public string? EmailLastError { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid recipientId,
        NotificationType type,
        Guid? taskId,
        Guid? projectId,
        string subject,
        string actorName,
        DateTimeOffset createdAt)
    {
        if (recipientId == Guid.Empty)
        {
            throw new ArgumentException("The recipient id must not be empty.", nameof(recipientId));
        }

        GuardReferences(type, taskId, projectId);

        Guard.NotNullOrWhiteSpace(subject);
        var trimmedSubject = subject.Trim();
        Guard.MaxLength(trimmedSubject, SubjectMaxLength, nameof(subject));

        Guard.NotNullOrWhiteSpace(actorName);
        var trimmedActor = actorName.Trim();
        Guard.MaxLength(trimmedActor, ActorNameMaxLength, nameof(actorName));

        return new Notification(
            Guid.CreateVersion7(),
            recipientId,
            type,
            taskId,
            projectId,
            trimmedSubject,
            trimmedActor,
            createdAt,
            null,
            EmailDeliveryStatus.Pending,
            0,
            createdAt,
            null,
            null,
            null);
    }

    /// <summary>Returns true only on the first call; later calls keep the first read time.</summary>
    public bool MarkRead(DateTimeOffset at)
    {
        if (ReadAt is not null)
        {
            return false;
        }

        ReadAt = at;
        return true;
    }

    public void EmailSent(DateTimeOffset at)
    {
        EmailStatus = EmailDeliveryStatus.Sent;
        EmailSentAt = at;
        EmailClaimedUntil = null;
    }

    /// <summary>Counts the attempt; fails for good at <paramref name="maxAttempts"/>, else retries after 2^attempts minutes.</summary>
    public void EmailFailed(string error, DateTimeOffset now, int maxAttempts)
    {
        Guard.NotNullOrWhiteSpace(error);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        EmailAttempts++;
        EmailLastError = error.Length > EmailErrorMaxLength ? error[..EmailErrorMaxLength] : error;
        EmailClaimedUntil = null;

        if (EmailAttempts >= maxAttempts)
        {
            EmailStatus = EmailDeliveryStatus.Failed;
            return;
        }

        EmailNextAttemptAt = now.AddMinutes(Math.Pow(2, EmailAttempts));
    }

    private static void GuardReferences(NotificationType type, Guid? taskId, Guid? projectId)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Unknown notification type.", nameof(type));
        }

        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("The task id must not be empty.", nameof(taskId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("The project id must not be empty.", nameof(projectId));
        }

        if (type == NotificationType.TaskAssigned && taskId is null)
        {
            throw new ArgumentException("TaskAssigned needs a task id.", nameof(taskId));
        }

        if (type == NotificationType.ProjectOwnerAssigned && projectId is null)
        {
            throw new ArgumentException("ProjectOwnerAssigned needs a project id.", nameof(projectId));
        }

        if (type is NotificationType.TaskUnassigned or NotificationType.ProjectOwnerRemoved
            && (taskId is not null || projectId is not null))
        {
            throw new ArgumentException("Removed notifications carry no task or project id.", nameof(type));
        }
    }
}
