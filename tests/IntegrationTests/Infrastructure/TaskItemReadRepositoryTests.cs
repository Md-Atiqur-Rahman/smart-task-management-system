using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListTasks;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using SortDirection = Datavanced.SmartTaskManagement.Application.Abstractions.Paging.SortDirection;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Task read side against a real SQL Server. The database is shared by the collection, so every
/// test creates its own users, projects and a unique marker and asserts only on its own rows.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class TaskItemReadRepositoryTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _marker = "m" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task List_AsTeamMember_ReturnsOnlyAssignedTasks()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var otherMember = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var mine = await SaveTaskAsync(project.Id, $"{_marker} mine", assigneeId: member.Id);
        await SaveTaskAsync(project.Id, $"{_marker} other", assigneeId: otherMember.Id);
        await SaveTaskAsync(project.Id, $"{_marker} nobody");

        var page = await ListAsync(Query(search: _marker), member.Id, Role.TeamMember);

        page.TotalCount.ShouldBe(1);
        var item = page.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(mine.Id);
        item.AssigneeId.ShouldBe(member.Id);
        item.AssigneeName.ShouldBe(member.FullName);
        item.ProjectId.ShouldBe(project.Id);
        item.ProjectName.ShouldBe(project.Name);
    }

    [Fact]
    public async Task List_AsOwnerPm_ReturnsAllTasksOfOwnProjectsAndOwnAssignments()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var own = await SaveProjectAsync(pm.Id);
        var foreign = await SaveProjectAsync(otherPm.Id);
        var ownUnassigned = await SaveTaskAsync(own.Id, $"{_marker} own unassigned");
        var ownAssigned = await SaveTaskAsync(own.Id, $"{_marker} own assigned", assigneeId: member.Id);
        var foreignMine = await SaveTaskAsync(foreign.Id, $"{_marker} foreign mine", assigneeId: pm.Id);
        await SaveTaskAsync(foreign.Id, $"{_marker} foreign hidden", assigneeId: member.Id);
        await SaveTaskAsync(foreign.Id, $"{_marker} foreign unassigned");

        var page = await ListAsync(Query(search: _marker), pm.Id, Role.ProjectManager);

        page.TotalCount.ShouldBe(3);
        page.Items.Select(i => i.Id).ShouldBe([ownUnassigned.Id, ownAssigned.Id, foreignMine.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task List_InProjectAsNonManagerAssignee_ReturnsOnlyOwnTasks()
    {
        var owner = await SaveUserAsync(Role.ProjectManager);
        var pmAssignee = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(owner.Id);
        var mine = await SaveTaskAsync(project.Id, "mine", assigneeId: pmAssignee.Id);
        await SaveTaskAsync(project.Id, "someone else", assigneeId: member.Id);
        await SaveTaskAsync(project.Id, "nobody");

        var page = await ListAsync(Query(), pmAssignee.Id, Role.ProjectManager, project.Id);

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(mine.Id);

        // The owner, a manager of the project, sees all three through the same filter.
        (await ListAsync(Query(), owner.Id, Role.ProjectManager, project.Id)).TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task List_ProjectFilterForInvisibleProject_ReturnsEmptyPage()
    {
        var owner = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(owner.Id);
        await SaveTaskAsync(project.Id, "hidden");

        var page = await ListAsync(Query(), otherPm.Id, Role.ProjectManager, project.Id);

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_TaskInDeletedProject_IsAbsentForAdmin()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var live = await SaveProjectAsync(pm.Id);
        var deleted = await SaveProjectAsync(pm.Id);
        var liveTask = await SaveTaskAsync(live.Id, $"{_marker} live");
        var deletedProjectTask = await SaveTaskAsync(deleted.Id, $"{_marker} in deleted project", assigneeId: member.Id);
        await SoftDeleteProjectAsync(deleted.Id);

        var page = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(liveTask.Id);
        (await ListAsync(Query(search: _marker), member.Id, Role.TeamMember)).TotalCount.ShouldBe(0);
        (await GetByIdAsync(deletedProjectTask.Id, Guid.NewGuid(), Role.Admin)).ShouldBeNull();
        (await GetByIdAsync(deletedProjectTask.Id, member.Id, Role.TeamMember)).ShouldBeNull();
    }

    [Fact]
    public async Task List_SoftDeletedTask_IsAbsent()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);
        var live = await SaveTaskAsync(project.Id, $"{_marker} live");
        var deleted = await SaveTaskAsync(project.Id, $"{_marker} deleted");
        await SoftDeleteTaskAsync(deleted.Id);

        var page = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(live.Id);
        (await GetByIdAsync(deleted.Id, Guid.NewGuid(), Role.Admin)).ShouldBeNull();
    }

    [Fact]
    public async Task List_Unassigned_ReturnsOnlyTasksWithoutAssignee()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var free1 = await SaveTaskAsync(project.Id, "free 1");
        var free2 = await SaveTaskAsync(project.Id, "free 2");
        var taken = await SaveTaskAsync(project.Id, "taken", assigneeId: member.Id);

        var unassigned = await ListAsync(Query(unassigned: true), pm.Id, Role.ProjectManager, project.Id);
        var byAssignee = await ListAsync(Query(assigneeId: member.Id), pm.Id, Role.ProjectManager, project.Id);

        unassigned.TotalCount.ShouldBe(2);
        unassigned.Items.Select(i => i.Id).ShouldBe([free1.Id, free2.Id], ignoreOrder: true);
        unassigned.Items.ShouldAllBe(i => i.AssigneeId == null && i.AssigneeName == null);
        byAssignee.Items.ShouldHaveSingleItem().Id.ShouldBe(taken.Id);
    }

    [Fact]
    public async Task List_DueRange_IsInclusiveAndExcludesTasksWithoutDueDate()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);
        await SaveTaskAsync(project.Id, "before", dueDate: new DateOnly(2026, 11, 9));
        var onFrom = await SaveTaskAsync(project.Id, "on from", dueDate: new DateOnly(2026, 11, 10));
        var inside = await SaveTaskAsync(project.Id, "inside", dueDate: new DateOnly(2026, 11, 12));
        var onTo = await SaveTaskAsync(project.Id, "on to", dueDate: new DateOnly(2026, 11, 15));
        await SaveTaskAsync(project.Id, "after", dueDate: new DateOnly(2026, 11, 16));
        await SaveTaskAsync(project.Id, "no due date");

        var range = await ListAsync(
            Query(dueFrom: new DateOnly(2026, 11, 10), dueTo: new DateOnly(2026, 11, 15)),
            pm.Id, Role.ProjectManager, project.Id);
        var fromOnly = await ListAsync(Query(dueFrom: new DateOnly(2026, 11, 15)), pm.Id, Role.ProjectManager, project.Id);
        var toOnly = await ListAsync(Query(dueTo: new DateOnly(2026, 11, 9)), pm.Id, Role.ProjectManager, project.Id);

        range.TotalCount.ShouldBe(3);
        range.Items.Select(i => i.Id).ShouldBe([onFrom.Id, inside.Id, onTo.Id], ignoreOrder: true);
        fromOnly.TotalCount.ShouldBe(2);
        fromOnly.Items.ShouldAllBe(i => i.DueDate != null);
        toOnly.Items.ShouldHaveSingleItem().Title.ShouldBe("before");
    }

    [Fact]
    public async Task List_SearchMatchesTitleOrDescriptionLiterally()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);
        var byTitle = await SaveTaskAsync(project.Id, "Deal 50%_off now");
        var byDescription = await SaveTaskAsync(project.Id, "Other", description: "about 50%_off here");
        await SaveTaskAsync(project.Id, "Deal 50%xoff now");
        await SaveTaskAsync(project.Id, "Deal 50 off", description: "50 percent off");

        var page = await ListAsync(Query(search: "50%_off"), pm.Id, Role.ProjectManager, project.Id);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(i => i.Id).ShouldBe([byTitle.Id, byDescription.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task List_SortByPriority_UsesRankOrder()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);

        // Created in an order that is neither rank nor alphabetical order.
        var high = await SaveTaskAsync(project.Id, "high", priority: TaskPriority.High);
        var low = await SaveTaskAsync(project.Id, "low", priority: TaskPriority.Low);
        var critical = await SaveTaskAsync(project.Id, "critical", priority: TaskPriority.Critical);
        var medium = await SaveTaskAsync(project.Id, "medium", priority: TaskPriority.Medium);
        Guid[] expected = [low.Id, medium.Id, high.Id, critical.Id];

        var ascending = await ListAsync(
            Query(sortBy: TaskSortFields.Priority), pm.Id, Role.ProjectManager, project.Id);
        var descending = await ListAsync(
            Query(sortBy: TaskSortFields.Priority, direction: SortDirection.Descending), pm.Id, Role.ProjectManager, project.Id);

        ascending.Items.Select(i => i.Id).ShouldBe(expected);
        descending.Items.Select(i => i.Id).ShouldBe(Enumerable.Reverse(expected));
    }

    [Fact]
    public async Task List_SortByStatus_UsesRankOrder()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);
        var completed = await SaveTaskAsync(project.Id, "completed", status: TaskItemStatus.Completed);
        var toDo = await SaveTaskAsync(project.Id, "to do");
        var cancelled = await SaveTaskAsync(project.Id, "cancelled", status: TaskItemStatus.Cancelled);
        var inProgress = await SaveTaskAsync(project.Id, "in progress", status: TaskItemStatus.InProgress);
        Guid[] expected = [toDo.Id, inProgress.Id, completed.Id, cancelled.Id];

        var ascending = await ListAsync(
            Query(sortBy: TaskSortFields.Status), pm.Id, Role.ProjectManager, project.Id);
        var descending = await ListAsync(
            Query(sortBy: TaskSortFields.Status, direction: SortDirection.Descending), pm.Id, Role.ProjectManager, project.Id);

        ascending.Items.Select(i => i.Id).ShouldBe(expected);
        descending.Items.Select(i => i.Id).ShouldBe(Enumerable.Reverse(expected));
    }

    [Fact]
    public async Task List_SortByDueDate_PutsTasksWithoutDueDateLastAscending()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);
        var later = await SaveTaskAsync(project.Id, "later", dueDate: new DateOnly(2026, 12, 1));
        var none = await SaveTaskAsync(project.Id, "none");
        var sooner = await SaveTaskAsync(project.Id, "sooner", dueDate: new DateOnly(2026, 11, 20));

        var ascending = await ListAsync(
            Query(sortBy: TaskSortFields.DueDate), pm.Id, Role.ProjectManager, project.Id);
        var descending = await ListAsync(
            Query(sortBy: TaskSortFields.DueDate, direction: SortDirection.Descending), pm.Id, Role.ProjectManager, project.Id);

        ascending.Items.Select(i => i.Id).ShouldBe([sooner.Id, later.Id, none.Id]);
        descending.Items.Select(i => i.Id).ShouldBe([none.Id, later.Id, sooner.Id]);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    public async Task List_SortByPlainKey_OrdersAscendingAndDescending(string sortBy)
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);

        // Created in the order b, c, a; updated in the order c, a, b (distinct times for each).
        var b = await SaveTaskAsync(project.Id, "b");
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var c = await SaveTaskAsync(project.Id, "c");
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var a = await SaveTaskAsync(project.Id, "a");
        foreach (var task in new[] { c, a, b })
        {
            fixture.Time.Advance(TimeSpan.FromMinutes(1));
            await ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);
        }

        Guid[] expectedAscending = sortBy switch
        {
            TaskSortFields.Title => [a.Id, b.Id, c.Id],
            TaskSortFields.CreatedAt => [b.Id, c.Id, a.Id],
            _ => [c.Id, a.Id, b.Id],
        };

        var ascending = await ListAsync(Query(sortBy: sortBy), pm.Id, Role.ProjectManager, project.Id);
        var descending = await ListAsync(
            Query(sortBy: sortBy, direction: SortDirection.Descending), pm.Id, Role.ProjectManager, project.Id);

        ascending.Items.Select(i => i.Id).ShouldBe(expectedAscending);
        descending.Items.Select(i => i.Id).ShouldBe(Enumerable.Reverse(expectedAscending));
    }

    [Fact]
    public async Task List_PageTwo_ReturnsRemainingItemsAndTotalCount()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync(pm.Id);
        var first = await SaveTaskAsync(project.Id, "1");
        var second = await SaveTaskAsync(project.Id, "2");
        var third = await SaveTaskAsync(project.Id, "3");

        var page = await ListAsync(
            Query(sortBy: TaskSortFields.Title, page: 2, pageSize: 2), Guid.NewGuid(), Role.Admin, project.Id);

        page.TotalCount.ShouldBe(3);
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(third.Id);
        new[] { first.Id, second.Id }.ShouldNotContain(page.Items[0].Id);
    }

    [Fact]
    public async Task GetById_ReturnsAssigneeAndProject()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var outsider = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var task = await SaveTaskAsync(
            project.Id, $"{_marker} details", "Some description", TaskPriority.High,
            new DateOnly(2026, 11, 20), member.Id, TaskItemStatus.InProgress);
        var unassigned = await SaveTaskAsync(project.Id, $"{_marker} unassigned");

        var details = (await GetByIdAsync(task.Id, member.Id, Role.TeamMember)).ShouldNotBeNull();

        details.Id.ShouldBe(task.Id);
        details.Title.ShouldBe($"{_marker} details");
        details.Description.ShouldBe("Some description");
        details.Status.ShouldBe(TaskItemStatus.InProgress);
        details.Priority.ShouldBe(TaskPriority.High);
        details.DueDate.ShouldBe(new DateOnly(2026, 11, 20));
        details.Assignee.ShouldBe(new TaskAssigneeDto(member.Id, member.FullName, member.Email.Value));
        details.Project.ShouldBe(new TaskProjectDto(project.Id, project.Name));
        details.CreatedAt.ShouldNotBe(default);

        (await GetByIdAsync(task.Id, pm.Id, Role.ProjectManager)).ShouldNotBeNull();
        (await GetByIdAsync(task.Id, outsider.Id, Role.TeamMember)).ShouldBeNull();
        (await GetByIdAsync(unassigned.Id, member.Id, Role.TeamMember)).ShouldBeNull();
        (await GetByIdAsync(unassigned.Id, pm.Id, Role.ProjectManager)).ShouldNotBeNull().Assignee.ShouldBeNull();
    }

    [Fact]
    public async Task GetById_AssigneeSoftDeleted_StillReturnsAssigneeName()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(pm.Id);
        var task = await SaveTaskAsync(project.Id, $"{_marker} orphaned", assigneeId: member.Id);

        await using (var context = fixture.CreateDbContext())
        {
            var user = (await new UserRepository(context).GetByIdAsync(member.Id, _ct)).ShouldNotBeNull();
            context.Entry(user).Property(u => u.IsDeleted).CurrentValue = true;
            await context.SaveChangesAsync(_ct);
        }

        var details = (await GetByIdAsync(task.Id, Guid.NewGuid(), Role.Admin)).ShouldNotBeNull();
        details.Assignee.ShouldBe(new TaskAssigneeDto(member.Id, member.FullName, member.Email.Value));

        var page = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);
        page.Items.ShouldHaveSingleItem().AssigneeName.ShouldBe(member.FullName);
    }

    [Fact]
    public async Task IsProjectVisible_AssigneeOfOneTask_IsTrue_OtherTeamMember_IsFalse()
    {
        var owner = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var assignee = await SaveUserAsync(Role.TeamMember);
        var other = await SaveUserAsync(Role.TeamMember);
        var formerAssignee = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync(owner.Id);
        await SaveTaskAsync(project.Id, "assigned", assigneeId: assignee.Id);
        var deletedTask = await SaveTaskAsync(project.Id, "deleted", assigneeId: formerAssignee.Id);
        await SoftDeleteTaskAsync(deletedTask.Id);

        (await IsProjectVisibleAsync(project.Id, assignee.Id, Role.TeamMember)).ShouldBeTrue();
        (await IsProjectVisibleAsync(project.Id, other.Id, Role.TeamMember)).ShouldBeFalse();
        (await IsProjectVisibleAsync(project.Id, formerAssignee.Id, Role.TeamMember)).ShouldBeFalse();
        (await IsProjectVisibleAsync(project.Id, owner.Id, Role.ProjectManager)).ShouldBeTrue();
        (await IsProjectVisibleAsync(project.Id, otherPm.Id, Role.ProjectManager)).ShouldBeFalse();
        (await IsProjectVisibleAsync(project.Id, Guid.NewGuid(), Role.Admin)).ShouldBeTrue();
        (await IsProjectVisibleAsync(Guid.NewGuid(), Guid.NewGuid(), Role.Admin)).ShouldBeFalse();

        await SoftDeleteProjectAsync(project.Id);
        (await IsProjectVisibleAsync(project.Id, Guid.NewGuid(), Role.Admin)).ShouldBeFalse();
        (await IsProjectVisibleAsync(project.Id, assignee.Id, Role.TeamMember)).ShouldBeFalse();
    }

    private static ListTasksQuery Query(
        string? search = null,
        Guid? assigneeId = null,
        bool unassigned = false,
        DateOnly? dueFrom = null,
        DateOnly? dueTo = null,
        string? sortBy = null,
        SortDirection direction = SortDirection.Ascending,
        int page = 1,
        int pageSize = 20) =>
        new(search, null, null, assigneeId, unassigned, dueFrom, dueTo, sortBy, direction, page, pageSize, null);

    private async Task<PagedResult<TaskListItemDto>> ListAsync(
        ListTasksQuery query, Guid callerId, Role role, Guid? projectId = null)
    {
        await using var context = fixture.CreateDbContext();
        return await new TaskItemReadRepository(context).ListAsync(query, projectId, callerId, role, _ct);
    }

    private async Task<TaskDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role role)
    {
        await using var context = fixture.CreateDbContext();
        return await new TaskItemReadRepository(context).GetByIdAsync(id, callerId, role, _ct);
    }

    private async Task<bool> IsProjectVisibleAsync(Guid projectId, Guid callerId, Role role)
    {
        await using var context = fixture.CreateDbContext();
        return await new TaskItemReadRepository(context).IsProjectVisibleAsync(projectId, callerId, role, _ct);
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
        var project = Project.Create($"{_marker} project {Guid.NewGuid():N}", null, ownerId);
        await using var context = fixture.CreateDbContext();
        new ProjectRepository(context).Add(project);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return project;
    }

    private async Task<TaskItem> SaveTaskAsync(
        Guid projectId,
        string title,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateOnly? dueDate = null,
        Guid? assigneeId = null,
        TaskItemStatus status = TaskItemStatus.ToDo)
    {
        var task = TaskItem.Create(projectId, title, description, priority, dueDate, assigneeId);
        task.ChangeStatus(status);
        await using var context = fixture.CreateDbContext();
        new TaskItemRepository(context).Add(task);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return task;
    }

    private async Task ChangeStatusAsync(Guid id, TaskItemStatus status)
    {
        await using var context = fixture.CreateDbContext();
        var task = (await new TaskItemRepository(context).GetByIdAsync(id, _ct)).ShouldNotBeNull();
        task.ChangeStatus(status).ShouldBeTrue();
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
