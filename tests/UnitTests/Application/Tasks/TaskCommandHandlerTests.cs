using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ChangeTaskStatus;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.CreateTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.DeleteTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.UpdateTask;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Tasks;

public sealed class TaskCommandHandlerTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ITaskItemRepository _tasks = Substitute.For<ITaskItemRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ISecurityContext _security = Substitute.For<ISecurityContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly IAssignmentNotifier _notifier = Substitute.For<IAssignmentNotifier>();
    private readonly ILiveEventPublisher _publisher = Substitute.For<ILiveEventPublisher>();
    private readonly LiveEventBuffer _liveEvents;

    public TaskCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _liveEvents = new LiveEventBuffer(_publisher);

        // Stands in for the real notifier: buffers one event so a test can see whether it was flushed.
        _notifier.WhenForAnyArgs(n => n.TaskAssigneeChangedAsync(default!, default, default, Arg.Any<CancellationToken>()))
            .Do(_ => _liveEvents.Add(new LiveEvent(LiveEventTypes.DashboardChanged, [], true, null)));
    }

    private Task AssertFlushedAsync(int times) =>
        _publisher.Received(times).PublishAsync(Arg.Any<IReadOnlyList<LiveEvent>>(), CancellationToken.None);

    private void SetCaller(Guid id, Role role)
    {
        _security.UserId.Returns(id);
        _security.Role.Returns(role);
    }

    private User NewUser(Role role)
    {
        var user = User.Register(Email.Create(Guid.NewGuid().ToString("N") + "@x.com"), "Name", "hash");
        if (role != user.Role)
        {
            user.ChangeRole(role);
        }

        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private Project NewProject(Guid ownerId)
    {
        var project = Project.Create("P", null, ownerId);
        _projects.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        return project;
    }

    private TaskItem NewTask(Guid projectId, Guid? assigneeId = null)
    {
        var task = TaskItem.Create(projectId, "T", null, TaskPriority.Medium, null, assigneeId);
        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        return task;
    }

    private async Task AssertNothingSavedAsync()
    {
        _tasks.DidNotReceive().Add(Arg.Any<TaskItem>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private CreateTaskCommandHandler CreateHandler() =>
        new(_security, _projects, _tasks, _users, _unitOfWork, _notifier, _liveEvents);

    private UpdateTaskCommandHandler UpdateHandler() =>
        new(_security, _tasks, _projects, _unitOfWork, _notifier, _liveEvents);

    private ChangeTaskStatusCommandHandler StatusHandler() =>
        new(_security, _tasks, _projects, _unitOfWork, _notifier, _liveEvents);

    private AssignTaskCommandHandler AssignHandler() =>
        new(_security, _tasks, _projects, _users, _unitOfWork, _notifier, _liveEvents);

    private DeleteTaskCommandHandler DeleteHandler() =>
        new(_security, _tasks, _projects, _unitOfWork, _time, _notifier, _liveEvents);

    [Fact]
    public async Task Create_AsOwnerPm_AddsTaskWithMediumPriorityWhenNoneSent()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var project = NewProject(pm);
        TaskItem? added = null;
        _tasks.When(r => r.Add(Arg.Any<TaskItem>())).Do(c => added = c.Arg<TaskItem>());

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, null, null, null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.Priority.ShouldBe(TaskPriority.Medium);
        added.ProjectId.ShouldBe(project.Id);
        result.Value.ShouldBe(added.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InAnotherPmsProject_ReturnsProjectNotFound()
    {
        SetCaller(Guid.NewGuid(), Role.ProjectManager);
        var project = NewProject(Guid.NewGuid());

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, null, null, null), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_InDeletedProject_ReturnsProjectNotFound()
    {
        // The write repository returns null for a soft-deleted project.
        SetCaller(Guid.NewGuid(), Role.Admin);

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(Guid.NewGuid(), "T", null, null, null, null), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_PmAssigningAnotherPm_ReturnsAssigneeNotAllowed()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var project = NewProject(pm);
        var otherPm = NewUser(Role.ProjectManager);

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, null, null, otherPm.Id), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.AssigneeNotAllowed);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_AdminAssigningAnotherPm_Succeeds()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var project = NewProject(Guid.NewGuid());
        var pm = NewUser(Role.ProjectManager);

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, TaskPriority.High, null, pm.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _tasks.Received(1).Add(Arg.Is<TaskItem>(t => t.AssigneeId == pm.Id && t.Priority == TaskPriority.High));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UnknownAssignee_ReturnsInvalidAssignee()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var project = NewProject(pm);

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, null, null, Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.InvalidAssignee);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_PmAssigningTeamMemberOrSelf_Succeeds()
    {
        var pm = NewUser(Role.ProjectManager);
        SetCaller(pm.Id, Role.ProjectManager);
        var project = NewProject(pm.Id);
        var member = NewUser(Role.TeamMember);

        var toMember = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, null, null, member.Id), CancellationToken.None);
        var toSelf = await CreateHandler().Handle(
            new CreateTaskCommand(project.Id, "T", null, null, null, pm.Id), CancellationToken.None);

        toMember.IsSuccess.ShouldBeTrue();
        toSelf.IsSuccess.ShouldBeTrue();
        _tasks.Received(2).Add(Arg.Any<TaskItem>());
    }

    [Fact]
    public async Task Create_NoUserIdInSecurityContext_ReturnsTokenRevoked()
    {
        _security.UserId.Returns((Guid?)null);
        _security.Role.Returns(Role.Admin);

        var result = await CreateHandler().Handle(
            new CreateTaskCommand(Guid.NewGuid(), "T", null, null, null, null), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Update_AsOwnerPm_UpdatesAndSaves()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var task = NewTask(NewProject(pm).Id);
        var due = new DateOnly(2026, 12, 1);

        var result = await UpdateHandler().Handle(
            new UpdateTaskCommand(task.Id, " New ", "d", TaskPriority.Critical, due), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        task.Title.ShouldBe("New");
        task.Description.ShouldBe("d");
        task.Priority.ShouldBe(TaskPriority.Critical);
        task.DueDate.ShouldBe(due);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_AssigneePmNotManager_ReturnsNotFound()
    {
        var caller = Guid.NewGuid();
        SetCaller(caller, Role.ProjectManager);
        var task = NewTask(NewProject(Guid.NewGuid()).Id, caller);

        var result = await UpdateHandler().Handle(
            new UpdateTaskCommand(task.Id, "New", null, TaskPriority.Low, null), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        task.Title.ShouldBe("T");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Update_TaskInDeletedProject_ReturnsNotFound()
    {
        // Admin manages every project, but the project of the task is soft-deleted (repository returns null).
        SetCaller(Guid.NewGuid(), Role.Admin);
        var task = NewTask(Guid.NewGuid());

        var result = await UpdateHandler().Handle(
            new UpdateTaskCommand(task.Id, "New", null, TaskPriority.Low, null), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Update_UnknownTask_ReturnsNotFound()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);

        var result = await UpdateHandler().Handle(
            new UpdateTaskCommand(Guid.NewGuid(), "New", null, TaskPriority.Low, null), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task ChangeStatus_AssignedTeamMember_Succeeds()
    {
        var member = Guid.NewGuid();
        SetCaller(member, Role.TeamMember);
        var task = NewTask(NewProject(Guid.NewGuid()).Id, member);

        var result = await StatusHandler().Handle(
            new ChangeTaskStatusCommand(task.Id, TaskItemStatus.InProgress), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskItemStatus.InProgress);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatus_TeamMemberNotAssigned_ReturnsNotFound()
    {
        SetCaller(Guid.NewGuid(), Role.TeamMember);
        var task = NewTask(NewProject(Guid.NewGuid()).Id, Guid.NewGuid());

        var result = await StatusHandler().Handle(
            new ChangeTaskStatusCommand(task.Id, TaskItemStatus.InProgress), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        task.Status.ShouldBe(TaskItemStatus.ToDo);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task ChangeStatus_CompletedToToDo_Succeeds()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var task = NewTask(NewProject(pm).Id);
        task.ChangeStatus(TaskItemStatus.Completed);

        var result = await StatusHandler().Handle(
            new ChangeTaskStatusCommand(task.Id, TaskItemStatus.ToDo), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskItemStatus.ToDo);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_SucceedsWithoutSave()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var task = NewTask(NewProject(Guid.NewGuid()).Id);

        var result = await StatusHandler().Handle(
            new ChangeTaskStatusCommand(task.Id, TaskItemStatus.ToDo), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task ChangeStatus_TaskInDeletedProject_ReturnsNotFoundEvenForAssignee()
    {
        var member = Guid.NewGuid();
        SetCaller(member, Role.TeamMember);
        var task = NewTask(Guid.NewGuid(), member);

        var result = await StatusHandler().Handle(
            new ChangeTaskStatusCommand(task.Id, TaskItemStatus.Completed), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Assign_Null_UnassignsAndSaves()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var task = NewTask(NewProject(pm).Id, Guid.NewGuid());

        var result = await AssignHandler().Handle(new AssignTaskCommand(task.Id, null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        task.AssigneeId.ShouldBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assign_ToOther_PassesPreviousAssigneeToNotifierAndFlushesAfterSave()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var previous = Guid.NewGuid();
        var member = NewUser(Role.TeamMember);
        var task = NewTask(NewProject(pm).Id, previous);

        var result = await AssignHandler().Handle(new AssignTaskCommand(task.Id, member.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _notifier.Received(1).TaskAssigneeChangedAsync(task, previous, pm, Arg.Any<CancellationToken>());
        await AssertFlushedAsync(1);
    }

    [Fact]
    public async Task Assign_SaveFails_ReturnsErrorAndPublishesNothing()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var member = NewUser(Role.TeamMember);
        var task = NewTask(NewProject(pm).Id);
        var conflict = Error.Conflict("concurrency.conflict", "Changed by someone else.");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(conflict));

        var result = await AssignHandler().Handle(new AssignTaskCommand(task.Id, member.Id), CancellationToken.None);

        result.Error.ShouldBe(conflict);
        await _notifier.Received(1).TaskAssigneeChangedAsync(task, null, pm, Arg.Any<CancellationToken>());
        await AssertFlushedAsync(0);
    }

    [Fact]
    public async Task Assign_SameAssignee_SucceedsWithoutSave()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var member = NewUser(Role.TeamMember);
        var task = NewTask(NewProject(pm).Id, member.Id);

        var result = await AssignHandler().Handle(new AssignTaskCommand(task.Id, member.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        task.AssigneeId.ShouldBe(member.Id);
        await AssertNothingSavedAsync();
        await _notifier.DidNotReceiveWithAnyArgs().TaskAssigneeChangedAsync(default!, default, default, Arg.Any<CancellationToken>());
        await AssertFlushedAsync(0);
    }

    [Fact]
    public async Task Assign_PmAssigningAdmin_ReturnsAssigneeNotAllowed()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var admin = NewUser(Role.Admin);
        var task = NewTask(NewProject(pm).Id);

        var result = await AssignHandler().Handle(new AssignTaskCommand(task.Id, admin.Id), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.AssigneeNotAllowed);
        task.AssigneeId.ShouldBeNull();
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Assign_AsAssigneeNotManager_ReturnsNotFound()
    {
        var member = Guid.NewGuid();
        SetCaller(member, Role.TeamMember);
        var task = NewTask(NewProject(Guid.NewGuid()).Id, member);

        var result = await AssignHandler().Handle(new AssignTaskCommand(task.Id, null), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        task.AssigneeId.ShouldBe(member);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Delete_AsOwnerPm_SoftDeletesWithTimeProviderTime()
    {
        var pm = Guid.NewGuid();
        SetCaller(pm, Role.ProjectManager);
        var task = NewTask(NewProject(pm).Id);

        var result = await DeleteHandler().Handle(new DeleteTaskCommand(task.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        task.IsDeleted.ShouldBeTrue();
        task.DeletedAt.ShouldBe(_time.GetUtcNow());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_AsAssigneeNotManager_ReturnsNotFound()
    {
        var caller = Guid.NewGuid();
        SetCaller(caller, Role.ProjectManager);
        var task = NewTask(NewProject(Guid.NewGuid()).Id, caller);

        var result = await DeleteHandler().Handle(new DeleteTaskCommand(task.Id), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        task.IsDeleted.ShouldBeFalse();
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Delete_NoRoleInSecurityContext_ReturnsTokenRevoked()
    {
        _security.UserId.Returns(Guid.NewGuid());
        _security.Role.Returns((Role?)null);

        var result = await DeleteHandler().Handle(new DeleteTaskCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
        await AssertNothingSavedAsync();
    }
}
