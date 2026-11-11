using Datavanced.SmartTaskManagement.Application.Features.Dashboard;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Dashboard read side against a real SQL Server. The database is shared, so every test creates its
/// own users and projects and asserts on a Team Member's or a fresh Project Manager's dashboard
/// (a visible set that holds only the test's own data); Admin numbers are never asserted.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class DashboardReadRepositoryTests(SqlServerFixture fixture)
{
    private const int Days = 7;
    private const int Limit = 10;

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly DateOnly _today = DateOnly.FromDateTime(fixture.Time.GetUtcNow().UtcDateTime);

    [Fact]
    public async Task Summary_NewTeamMember_AllZeroWithAllKeys()
    {
        var member = await SaveUserAsync(Role.TeamMember);

        var summary = await SummaryAsync(member.Id, Role.TeamMember);

        summary.ShouldBe(new DashboardSummaryDto(
            0, 0, new TaskStatusCountsDto(0, 0, 0, 0), new TaskPriorityCountsDto(0, 0, 0, 0), 0, 0, 0));
        (await UpcomingAsync(member.Id, Role.TeamMember)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Summary_TeamMember_CountsOnlyAssignedTasks()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var other = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, priority: TaskPriority.High);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, priority: TaskPriority.Low, status: TaskItemStatus.InProgress);
        await SaveTaskAsync(project.Id, assigneeId: other.Id);
        await SaveTaskAsync(project.Id);

        var summary = await SummaryAsync(member.Id, Role.TeamMember);

        summary.TotalProjects.ShouldBe(1);
        summary.TotalTasks.ShouldBe(2);
        summary.TasksByStatus.ShouldBe(new TaskStatusCountsDto(1, 1, 0, 0));
        summary.TasksByPriority.ShouldBe(new TaskPriorityCountsDto(1, 0, 1, 0));
        summary.PendingTasks.ShouldBe(2);
    }

    [Fact]
    public async Task Summary_ProjectManager_OwnProjectsPlusOwnAssignmentElsewhere()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var own = await SaveProjectAsync(pm.Id);
        var foreign = await SaveProjectAsync(otherPm.Id);
        var hiddenForeign = await SaveProjectAsync(otherPm.Id);
        await SaveTaskAsync(own.Id);
        await SaveTaskAsync(own.Id, assigneeId: member.Id);
        await SaveTaskAsync(foreign.Id, assigneeId: pm.Id);
        await SaveTaskAsync(foreign.Id, assigneeId: member.Id);
        await SaveTaskAsync(hiddenForeign.Id, assigneeId: member.Id);

        var summary = await SummaryAsync(pm.Id, Role.ProjectManager);

        summary.TotalProjects.ShouldBe(2);
        summary.TotalTasks.ShouldBe(3);
    }

    [Fact]
    public async Task Summary_DeletedTaskAndTaskInDeletedProject_NotCounted()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var live = await SaveProjectAsync(pm.Id);
        var deleted = await SaveProjectAsync(pm.Id);
        var dueSoon = _today.AddDays(1);
        await SaveTaskAsync(live.Id, assigneeId: member.Id, dueDate: dueSoon);
        var deletedTask = await SaveTaskAsync(live.Id, assigneeId: member.Id, dueDate: dueSoon);
        await SaveTaskAsync(deleted.Id, assigneeId: member.Id, dueDate: dueSoon);
        await SoftDeleteTaskAsync(deletedTask.Id);
        await SoftDeleteProjectAsync(deleted.Id);

        var summary = await SummaryAsync(member.Id, Role.TeamMember);

        summary.TotalTasks.ShouldBe(1);
        summary.TotalProjects.ShouldBe(1);
        (await UpcomingAsync(member.Id, Role.TeamMember)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Summary_CompletedPendingOverdue_CancelledInNeither()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var yesterday = _today.AddDays(-1);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.ToDo);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.InProgress);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Completed);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Cancelled, dueDate: yesterday);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.ToDo, dueDate: yesterday);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Completed, dueDate: yesterday);

        var summary = await SummaryAsync(member.Id, Role.TeamMember);

        summary.TotalTasks.ShouldBe(6);
        summary.TasksByStatus.ShouldBe(new TaskStatusCountsDto(2, 1, 2, 1));
        summary.CompletedTasks.ShouldBe(2);
        summary.PendingTasks.ShouldBe(3);
        summary.OverdueTasks.ShouldBe(1);
    }

    [Fact]
    public async Task Summary_UnassignedTask_DropsOutForPreviousAssignee()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var task = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(1));
        (await SummaryAsync(member.Id, Role.TeamMember)).TotalTasks.ShouldBe(1);
        (await UpcomingAsync(member.Id, Role.TeamMember)).Count.ShouldBe(1);

        await UnassignAsync(task.Id);

        var summary = await SummaryAsync(member.Id, Role.TeamMember);
        summary.TotalTasks.ShouldBe(0);
        summary.TotalProjects.ShouldBe(0);
        (await UpcomingAsync(member.Id, Role.TeamMember)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Upcoming_WindowEdgesInclusive_BeyondExcluded_OverdueExcluded()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var onToday = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today);
        var onEdge = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(Days));
        await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(Days + 1));
        await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(-1));
        await SaveTaskAsync(project.Id, assigneeId: member.Id);

        var upcoming = await UpcomingAsync(member.Id, Role.TeamMember);

        upcoming.Select(u => u.Id).ShouldBe([onToday.Id, onEdge.Id]);
        (await SummaryAsync(member.Id, Role.TeamMember)).OverdueTasks.ShouldBe(1);
    }

    [Fact]
    public async Task Upcoming_CompletedAndCancelled_Excluded()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var due = _today.AddDays(2);
        var todo = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: due);
        var inProgress = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: due, status: TaskItemStatus.InProgress);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: due, status: TaskItemStatus.Completed);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: due, status: TaskItemStatus.Cancelled);

        var upcoming = await UpcomingAsync(member.Id, Role.TeamMember);

        upcoming.Select(u => u.Id).ShouldBe([todo.Id, inProgress.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Upcoming_OrderedByDateThenPriority_LimitRespected()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var tomorrow = _today.AddDays(1);
        var sameDayLow = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: tomorrow, priority: TaskPriority.Low);
        var sameDayCritical = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: tomorrow, priority: TaskPriority.Critical);
        var sameDayHigh = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: tomorrow, priority: TaskPriority.High);
        var earliest = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today, priority: TaskPriority.Low);
        for (var i = 0; i < 6; i++)
        {
            await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(2 + i));
        }

        // Two more qualifying tasks beyond the limit: the latest dates, cut off.
        var cutOff1 = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(Days));
        var cutOff2 = await SaveTaskAsync(project.Id, assigneeId: member.Id, dueDate: _today.AddDays(Days));

        var upcoming = await UpcomingAsync(member.Id, Role.TeamMember);

        upcoming.Count.ShouldBe(Limit);
        upcoming.Take(4).Select(u => u.Id).ShouldBe(
            [earliest.Id, sameDayCritical.Id, sameDayHigh.Id, sameDayLow.Id]);
        upcoming.Select(u => u.DueDate).ShouldBe(upcoming.Select(u => u.DueDate).Order());
        upcoming.Select(u => u.Id).Count(id => id == cutOff1.Id || id == cutOff2.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Upcoming_ReturnsProjectAndAssigneeNames()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var task = await SaveTaskAsync(
            project.Id, assigneeId: member.Id, dueDate: _today.AddDays(3), priority: TaskPriority.High);

        var item = (await UpcomingAsync(pm.Id, Role.ProjectManager)).ShouldHaveSingleItem();

        item.ShouldBe(new UpcomingTaskDto(
            task.Id, task.Title, _today.AddDays(3), TaskItemStatus.ToDo, TaskPriority.High,
            project.Id, project.Name, member.Id, member.FullName));
    }

    private async Task<DashboardSummaryDto> SummaryAsync(Guid callerId, Role role)
    {
        await using var context = fixture.CreateDbContext();
        return await new DashboardReadRepository(context).GetSummaryAsync(callerId, role, _today, _ct);
    }

    private async Task<IReadOnlyList<UpcomingTaskDto>> UpcomingAsync(Guid callerId, Role role)
    {
        await using var context = fixture.CreateDbContext();
        return await new DashboardReadRepository(context).GetUpcomingAsync(callerId, role, _today, Days, Limit, _ct);
    }

    private async Task<User> SaveUserAsync(Role role)
    {
        var user = User.Register(
            Email.Create($"user.{Guid.NewGuid():N}@example.com"), $"User {Guid.NewGuid():N}", "hash");
        if (user.Role != role)
        {
            user.ChangeRole(role);
        }

        await using var context = fixture.CreateDbContext();
        new UserRepository(context).Add(user);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return user;
    }

    private async Task<Project> SaveProjectAsync(Guid ownerId)
    {
        var project = Project.Create($"dashboard project {Guid.NewGuid():N}", null, ownerId);
        await using var context = fixture.CreateDbContext();
        new ProjectRepository(context).Add(project);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return project;
    }

    private async Task<TaskItem> SaveTaskAsync(
        Guid projectId,
        TaskPriority priority = TaskPriority.Medium,
        DateOnly? dueDate = null,
        Guid? assigneeId = null,
        TaskItemStatus status = TaskItemStatus.ToDo)
    {
        var task = TaskItem.Create(projectId, $"task {Guid.NewGuid():N}", null, priority, dueDate, assigneeId);
        task.ChangeStatus(status);
        await using var context = fixture.CreateDbContext();
        new TaskItemRepository(context).Add(task);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return task;
    }

    private async Task UnassignAsync(Guid id)
    {
        await using var context = fixture.CreateDbContext();
        var task = (await new TaskItemRepository(context).GetByIdAsync(id, _ct)).ShouldNotBeNull();
        task.Assign(null).ShouldBeTrue();
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }

    private async Task SoftDeleteTaskAsync(Guid id)
    {
        await using var context = fixture.CreateDbContext();
        var task = (await new TaskItemRepository(context).GetByIdAsync(id, _ct)).ShouldNotBeNull();
        task.Delete(fixture.Time.GetUtcNow());
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }

    private async Task SoftDeleteProjectAsync(Guid id)
    {
        await using var context = fixture.CreateDbContext();
        var project = (await new ProjectRepository(context).GetByIdAsync(id, _ct)).ShouldNotBeNull();
        project.Delete(fixture.Time.GetUtcNow());
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }
}
