using Datavanced.SmartTaskManagement.Domain.Notifications;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain.Notifications;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private static Notification NewNotification() =>
        Notification.Create(Guid.NewGuid(), NotificationType.TaskAssigned, Guid.NewGuid(), null, "Fix login", "Alice", Now);

    [Fact]
    public void Create_TaskAssigned_PendingEmailAndUnread()
    {
        var recipientId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var notification = Notification.Create(
            recipientId, NotificationType.TaskAssigned, taskId, null, "  Fix login  ", "  Alice  ", Now);

        notification.Id.ShouldNotBe(Guid.Empty);
        notification.RecipientId.ShouldBe(recipientId);
        notification.TaskId.ShouldBe(taskId);
        notification.ProjectId.ShouldBeNull();
        notification.Subject.ShouldBe("Fix login");
        notification.ActorName.ShouldBe("Alice");
        notification.CreatedAt.ShouldBe(Now);
        notification.IsRead.ShouldBeFalse();
        notification.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        notification.EmailAttempts.ShouldBe(0);
        notification.EmailNextAttemptAt.ShouldBe(Now);
        notification.EmailClaimedUntil.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Create_InvalidArguments_Throws(Guid recipientId, NotificationType type, Guid? taskId, Guid? projectId, string subject)
    {
        Should.Throw<ArgumentException>(() =>
            Notification.Create(recipientId, type, taskId, projectId, subject, "Alice", Now));
    }

    public static TheoryData<Guid, NotificationType, Guid?, Guid?, string> InvalidArguments() => new()
    {
        { Guid.NewGuid(), NotificationType.TaskAssigned, null, null, "Subject" },
        { Guid.NewGuid(), NotificationType.ProjectOwnerAssigned, null, null, "Subject" },
        { Guid.NewGuid(), NotificationType.TaskUnassigned, Guid.NewGuid(), null, "Subject" },
        { Guid.Empty, NotificationType.TaskAssigned, Guid.NewGuid(), null, "Subject" },
        { Guid.NewGuid(), NotificationType.TaskAssigned, Guid.NewGuid(), null, "  " },
    };

    [Fact]
    public void Create_LongSubject_IsRejected()
    {
        Should.Throw<ArgumentException>(() =>
            Notification.Create(
                Guid.NewGuid(), NotificationType.TaskAssigned, Guid.NewGuid(), null, new string('a', Notification.SubjectMaxLength + 1), "Alice", Now));
    }

    [Fact]
    public void MarkRead_Twice_SecondReturnsFalseAndKeepsFirstTime()
    {
        var notification = NewNotification();

        notification.MarkRead(Now.AddMinutes(1)).ShouldBeTrue();
        notification.MarkRead(Now.AddMinutes(5)).ShouldBeFalse();

        notification.IsRead.ShouldBeTrue();
        notification.ReadAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void EmailFailed_BeforeMax_SchedulesBackoff()
    {
        var notification = NewNotification();

        notification.EmailFailed("boom", Now, 5);
        notification.EmailAttempts.ShouldBe(1);
        notification.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        notification.EmailNextAttemptAt.ShouldBe(Now.AddMinutes(2));

        notification.EmailFailed("boom", Now, 5);
        notification.EmailAttempts.ShouldBe(2);
        notification.EmailNextAttemptAt.ShouldBe(Now.AddMinutes(4));
        notification.EmailLastError.ShouldBe("boom");
        notification.EmailClaimedUntil.ShouldBeNull();
    }

    [Fact]
    public void EmailFailed_AtMax_IsFailed()
    {
        var notification = NewNotification();

        for (var i = 0; i < 5; i++)
        {
            notification.EmailFailed("boom", Now, 5);
        }

        notification.EmailAttempts.ShouldBe(5);
        notification.EmailStatus.ShouldBe(EmailDeliveryStatus.Failed);
    }

    [Fact]
    public void EmailFailed_LongError_IsTruncated()
    {
        var notification = NewNotification();

        notification.EmailFailed(new string('x', 1500), Now, 5);

        notification.EmailLastError!.Length.ShouldBe(Notification.EmailErrorMaxLength);
    }

    [Fact]
    public void EmailSent_SetsSentAndStatus()
    {
        var notification = NewNotification();

        notification.EmailSent(Now.AddMinutes(1));

        notification.EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
        notification.EmailSentAt.ShouldBe(Now.AddMinutes(1));
        notification.EmailClaimedUntil.ShouldBeNull();
    }
}
