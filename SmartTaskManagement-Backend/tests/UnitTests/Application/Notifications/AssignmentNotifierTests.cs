using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Notifications;

public sealed class AssignmentNotifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 30, 0, TimeSpan.Zero);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ITaskItemReadRepository _taskReads = Substitute.For<ITaskItemReadRepository>();
    private readonly ILiveEventPublisher _publisher = Substitute.For<ILiveEventPublisher>();
    private readonly List<Notification> _added = [];
    private readonly List<LiveEvent> _published = [];
    private readonly LiveEventBuffer _buffer;
    private readonly AssignmentNotifier _notifier;
    private readonly User _actor;

    public AssignmentNotifierTests()
    {
        _notifications.When(r => r.Add(Arg.Any<Notification>())).Do(c => _added.Add(c.Arg<Notification>()));
        _publisher.When(p => p.PublishAsync(Arg.Any<IReadOnlyList<LiveEvent>>(), Arg.Any<CancellationToken>()))
            .Do(c => _published.AddRange(c.Arg<IReadOnlyList<LiveEvent>>()));
        _buffer = new LiveEventBuffer(_publisher);
        _actor = User.Register(Email.Create($"{Guid.NewGuid():N}@x.com"), "Alice Actor", "hash");
        _users.GetByIdAsync(_actor.Id, Arg.Any<CancellationToken>()).Returns(_actor);
        _notifier = new AssignmentNotifier(
            _notifications, _users, _projects, _taskReads, _buffer, new FakeTimeProvider(Now));
    }

    private Project NewProject(Guid ownerId)
    {
        var project = Project.Create("Apollo", null, ownerId);
        _projects.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        return project;
    }

    private static TaskItem NewTask(Guid projectId, Guid? assigneeId) =>
        TaskItem.Create(projectId, "Fix login", null, TaskPriority.Medium, null, assigneeId);

    private async Task<IReadOnlyList<LiveEvent>> FlushAsync()
    {
        await _buffer.FlushAsync(_ct);
        return _published;
    }

    private static LiveEvent Dashboard(IReadOnlyList<LiveEvent> events) =>
        events.Single(e => e.Type == LiveEventTypes.DashboardChanged);

    private static List<LiveEvent> NotificationEvents(IReadOnlyList<LiveEvent> events) =>
        [.. events.Where(e => e.Type == LiveEventTypes.Notification)];

    [Fact]
    public async Task TaskAssigned_ToOther_NotifiesAssigneeAndBuffersEvents()
    {
        var owner = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var project = NewProject(owner);
        var task = NewTask(project.Id, assignee);

        await _notifier.TaskAssigneeChangedAsync(task, null, _actor.Id, _ct);

        var notification = _added.ShouldHaveSingleItem();
        notification.RecipientId.ShouldBe(assignee);
        notification.Type.ShouldBe(NotificationType.TaskAssigned);
        notification.TaskId.ShouldBe(task.Id);
        notification.Subject.ShouldBe("Fix login");
        notification.ActorName.ShouldBe("Alice Actor");
        notification.CreatedAt.ShouldBe(Now);

        var events = await FlushAsync();
        var live = NotificationEvents(events).ShouldHaveSingleItem();
        live.UserIds.ShouldBe([assignee]);
        live.IncludeAdmins.ShouldBeFalse();
        var dto = live.Notification.ShouldNotBeNull();
        dto.Id.ShouldBe(notification.Id);
        dto.Type.ShouldBe(NotificationType.TaskAssigned);
        dto.Message.ShouldBe("Alice Actor assigned you the task \"Fix login\".");
        dto.TaskId.ShouldBe(task.Id);
        dto.ProjectId.ShouldBeNull();
        dto.CreatedAt.ShouldBe(Now);
        dto.IsRead.ShouldBeFalse();

        var dashboard = Dashboard(events);
        dashboard.IncludeAdmins.ShouldBeTrue();
        dashboard.Notification.ShouldBeNull();
        dashboard.UserIds.ShouldBe([assignee, owner, _actor.Id], ignoreOrder: true);
    }

    // Review Focus 1
    [Fact]
    public async Task TaskAssigned_ToActor_CreatesNoNotification()
    {
        var project = NewProject(_actor.Id);
        var task = NewTask(project.Id, _actor.Id);

        await _notifier.TaskAssigneeChangedAsync(task, null, _actor.Id, _ct);

        _added.ShouldBeEmpty();
        var events = await FlushAsync();
        NotificationEvents(events).ShouldBeEmpty();
        Dashboard(events).UserIds.ShouldBe([_actor.Id]);
    }

    // Review Focus 2
    [Fact]
    public async Task TaskReassigned_NotifiesBoth_DashboardToBothOwnerAdminsActor()
    {
        var owner = Guid.NewGuid();
        var previous = Guid.NewGuid();
        var next = Guid.NewGuid();
        var project = NewProject(owner);
        var task = NewTask(project.Id, next);

        await _notifier.TaskAssigneeChangedAsync(task, previous, _actor.Id, _ct);

        _added.Count.ShouldBe(2);
        var assigned = _added.Single(n => n.RecipientId == next);
        assigned.Type.ShouldBe(NotificationType.TaskAssigned);
        assigned.TaskId.ShouldBe(task.Id);
        var unassigned = _added.Single(n => n.RecipientId == previous);
        unassigned.Type.ShouldBe(NotificationType.TaskUnassigned);
        unassigned.TaskId.ShouldBeNull();
        unassigned.ProjectId.ShouldBeNull();
        unassigned.Subject.ShouldBe("Fix login");

        var events = await FlushAsync();
        NotificationEvents(events).Select(e => e.UserIds.Single()).ShouldBe([next, previous], ignoreOrder: true);
        var dashboard = Dashboard(events);
        dashboard.IncludeAdmins.ShouldBeTrue();
        dashboard.UserIds.ShouldBe([previous, next, owner, _actor.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task TaskUnassigned_NotifiesPreviousOnly_NoLink()
    {
        var owner = Guid.NewGuid();
        var previous = Guid.NewGuid();
        var project = NewProject(owner);
        var task = NewTask(project.Id, null);

        await _notifier.TaskAssigneeChangedAsync(task, previous, _actor.Id, _ct);

        var notification = _added.ShouldHaveSingleItem();
        notification.RecipientId.ShouldBe(previous);
        notification.Type.ShouldBe(NotificationType.TaskUnassigned);
        notification.TaskId.ShouldBeNull();
        notification.ProjectId.ShouldBeNull();

        var events = await FlushAsync();
        var dto = NotificationEvents(events).ShouldHaveSingleItem().Notification.ShouldNotBeNull();
        dto.Message.ShouldBe("Alice Actor removed the task \"Fix login\" from you.");
        dto.TaskId.ShouldBeNull();
        Dashboard(events).UserIds.ShouldBe([previous, owner, _actor.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ProjectCreatedByAdminForPm_NotifiesOwner()
    {
        var pm = Guid.NewGuid();
        var project = Project.Create("Apollo", null, pm);

        await _notifier.ProjectOwnerChangedAsync(project, null, _actor.Id, _ct);

        var notification = _added.ShouldHaveSingleItem();
        notification.RecipientId.ShouldBe(pm);
        notification.Type.ShouldBe(NotificationType.ProjectOwnerAssigned);
        notification.ProjectId.ShouldBe(project.Id);
        notification.TaskId.ShouldBeNull();
        notification.Subject.ShouldBe("Apollo");

        var events = await FlushAsync();
        NotificationEvents(events).ShouldHaveSingleItem().Notification.ShouldNotBeNull()
            .Message.ShouldBe("Alice Actor made you the owner of the project \"Apollo\".");
        var dashboard = Dashboard(events);
        dashboard.IncludeAdmins.ShouldBeTrue();
        dashboard.UserIds.ShouldBe([pm, _actor.Id], ignoreOrder: true);
    }

    // Review Focus 1
    [Fact]
    public async Task ProjectCreatedByPmForSelf_CreatesNoNotification()
    {
        var project = Project.Create("Apollo", null, _actor.Id);

        await _notifier.ProjectOwnerChangedAsync(project, null, _actor.Id, _ct);

        _added.ShouldBeEmpty();
        var events = await FlushAsync();
        NotificationEvents(events).ShouldBeEmpty();
        Dashboard(events).UserIds.ShouldBe([_actor.Id]);
    }

    [Fact]
    public async Task OwnerChanged_NotifiesNewAndPreviousOwner()
    {
        var previous = Guid.NewGuid();
        var next = Guid.NewGuid();
        var project = Project.Create("Apollo", null, next);

        await _notifier.ProjectOwnerChangedAsync(project, previous, _actor.Id, _ct);

        _added.Count.ShouldBe(2);
        var assigned = _added.Single(n => n.RecipientId == next);
        assigned.Type.ShouldBe(NotificationType.ProjectOwnerAssigned);
        assigned.ProjectId.ShouldBe(project.Id);
        var removed = _added.Single(n => n.RecipientId == previous);
        removed.Type.ShouldBe(NotificationType.ProjectOwnerRemoved);
        removed.ProjectId.ShouldBeNull();
        removed.TaskId.ShouldBeNull();

        var events = await FlushAsync();
        NotificationEvents(events).Single(e => e.UserIds.Single() == previous).Notification.ShouldNotBeNull()
            .Message.ShouldBe("Alice Actor removed your ownership of the project \"Apollo\".");
        Dashboard(events).UserIds.ShouldBe([previous, next, _actor.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task ProjectDeleted_DashboardIncludesTaskAssignees()
    {
        var owner = Guid.NewGuid();
        var assigneeOne = Guid.NewGuid();
        var assigneeTwo = Guid.NewGuid();
        var project = Project.Create("Apollo", null, owner);
        _taskReads.GetAssigneeIdsAsync(project.Id, Arg.Any<CancellationToken>()).Returns([assigneeOne, assigneeTwo]);

        await _notifier.ProjectDeletedAsync(project, _actor.Id, _ct);

        _added.ShouldBeEmpty();
        var events = await FlushAsync();
        var dashboard = events.ShouldHaveSingleItem();
        dashboard.Type.ShouldBe(LiveEventTypes.DashboardChanged);
        dashboard.IncludeAdmins.ShouldBeTrue();
        dashboard.UserIds.ShouldBe([owner, assigneeOne, assigneeTwo, _actor.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task TaskChanged_DashboardOnlyToAssigneeOwnerAdminsActor()
    {
        var owner = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        var project = NewProject(owner);
        var task = NewTask(project.Id, assignee);

        await _notifier.TaskChangedAsync(task, _actor.Id, _ct);

        _added.ShouldBeEmpty();
        var events = await FlushAsync();
        var dashboard = events.ShouldHaveSingleItem();
        dashboard.Type.ShouldBe(LiveEventTypes.DashboardChanged);
        dashboard.IncludeAdmins.ShouldBeTrue();
        dashboard.UserIds.ShouldBe([assignee, owner, _actor.Id], ignoreOrder: true);
    }
}
