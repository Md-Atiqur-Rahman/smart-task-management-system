using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using SortDirection = Datavanced.SmartTaskManagement.Application.Abstractions.Paging.SortDirection;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Project read side against a real SQL Server. The database is shared by the collection,
/// so every test creates its own owners and a unique marker and asserts only on its own rows.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class ProjectReadRepositoryTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _marker = "m" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task List_AsProjectManager_ReturnsOnlyOwnProjects()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var mine1 = await SaveProjectAsync($"{_marker} mine 1", pm.Id);
        var mine2 = await SaveProjectAsync($"{_marker} mine 2", pm.Id);
        await SaveProjectAsync($"{_marker} other", otherPm.Id);

        var page = await ListAsync(Query(search: _marker), pm.Id, Role.ProjectManager);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(i => i.Id).ShouldBe([mine1.Id, mine2.Id], ignoreOrder: true);
        page.Items.ShouldAllBe(i => i.OwnerId == pm.Id && i.OwnerName == pm.FullName);
    }

    [Fact]
    public async Task List_AsProjectManagerFilteringByAnotherPm_ReturnsEmptyPage()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        await SaveProjectAsync($"{_marker} mine", pm.Id);
        await SaveProjectAsync($"{_marker} other", otherPm.Id);

        var page = await ListAsync(Query(ownerId: otherPm.Id), pm.Id, Role.ProjectManager);

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_AsAdminWithOwnerFilter_ReturnsThatOwnersProjects()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var first = await SaveProjectAsync($"{_marker} first", pm.Id);
        var second = await SaveProjectAsync($"{_marker} second", pm.Id);
        await SaveProjectAsync($"{_marker} other", otherPm.Id);

        var page = await ListAsync(Query(ownerId: pm.Id), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(i => i.Id).ShouldBe([first.Id, second.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task List_AsTeamMember_ReturnsEmptyPage()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        await SaveProjectAsync($"{_marker} hidden", pm.Id);

        var page = await ListAsync(Query(search: _marker), member.Id, Role.TeamMember);

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_SoftDeletedProject_IsAbsentForAdmin()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var live = await SaveProjectAsync($"{_marker} live", pm.Id);
        var deleted = await SaveProjectAsync($"{_marker} deleted", pm.Id);
        await SoftDeleteProjectAsync(deleted.Id);

        var page = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(live.Id);
    }

    [Fact]
    public async Task GetById_SoftDeletedProject_ReturnsNullForAdmin()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var deleted = await SaveProjectAsync($"{_marker} deleted", pm.Id);
        await SoftDeleteProjectAsync(deleted.Id);

        (await GetByIdAsync(deleted.Id, Guid.NewGuid(), Role.Admin)).ShouldBeNull();
    }

    [Fact]
    public async Task List_SearchMatchesNameOrDescription()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var byName = await SaveProjectAsync($"Alpha {_marker}", pm.Id);
        var byDescription = await SaveProjectAsync("Beta", pm.Id, $"about {_marker} here");
        await SaveProjectAsync("Gamma", pm.Id, "nothing");

        var page = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(i => i.Id).ShouldBe([byName.Id, byDescription.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task List_SearchWithWildcards_MatchesLiterally()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var literal = await SaveProjectAsync("Deal 50%_off[1]", pm.Id);
        await SaveProjectAsync("Deal 50%xoff1", pm.Id);
        await SaveProjectAsync("Deal 50 off 1", pm.Id);

        var page = await ListAsync(Query(search: "50%_off[1]", ownerId: pm.Id), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(literal.Id);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    public async Task List_SortByEachKey_OrdersAscendingAndDescending(string sortBy)
    {
        var pm = await SaveUserAsync(Role.ProjectManager);

        // Created in the order b, c, a; updated in the order c, a, b (distinct times for each).
        var b = await SaveProjectAsync($"{_marker} b", pm.Id);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var c = await SaveProjectAsync($"{_marker} c", pm.Id);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var a = await SaveProjectAsync($"{_marker} a", pm.Id);
        foreach (var project in new[] { c, a, b })
        {
            fixture.Time.Advance(TimeSpan.FromMinutes(1));
            await UpdateProjectAsync(project.Id);
        }

        Guid[] expectedAscending = sortBy switch
        {
            ProjectSortFields.Name => [a.Id, b.Id, c.Id],
            ProjectSortFields.CreatedAt => [b.Id, c.Id, a.Id],
            _ => [c.Id, a.Id, b.Id],
        };

        var ascending = await ListAsync(
            Query(search: _marker, sortBy: sortBy, direction: SortDirection.Ascending), pm.Id, Role.ProjectManager);
        var descending = await ListAsync(
            Query(search: _marker, sortBy: sortBy, direction: SortDirection.Descending), pm.Id, Role.ProjectManager);

        ascending.Items.Select(i => i.Id).ShouldBe(expectedAscending);
        descending.Items.Select(i => i.Id).ShouldBe(Enumerable.Reverse(expectedAscending));
    }

    [Fact]
    public async Task List_PageTwo_ReturnsRemainingItemsAndTotalCount()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var first = await SaveProjectAsync($"{_marker} 1", pm.Id);
        var second = await SaveProjectAsync($"{_marker} 2", pm.Id);
        var third = await SaveProjectAsync($"{_marker} 3", pm.Id);

        var page = await ListAsync(
            Query(ownerId: pm.Id, sortBy: ProjectSortFields.Name, page: 2, pageSize: 2), Guid.NewGuid(), Role.Admin);

        page.TotalCount.ShouldBe(3);
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(third.Id);
        new[] { first.Id, second.Id }.ShouldNotContain(page.Items[0].Id);
    }

    [Fact]
    public async Task GetById_AsAdmin_ReturnsOwnerNameAndEmail()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync($"{_marker} details", pm.Id, "Some description");

        var details = (await GetByIdAsync(project.Id, Guid.NewGuid(), Role.Admin)).ShouldNotBeNull();

        details.Id.ShouldBe(project.Id);
        details.Name.ShouldBe($"{_marker} details");
        details.Description.ShouldBe("Some description");
        details.Owner.ShouldBe(new ProjectOwnerDto(pm.Id, pm.FullName, pm.Email.Value));
        details.CreatedAt.ShouldNotBe(default);
        details.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetById_AsOwningProjectManager_ReturnsDetails()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync($"{_marker} own", pm.Id);

        (await GetByIdAsync(project.Id, pm.Id, Role.ProjectManager)).ShouldNotBeNull().Id.ShouldBe(project.Id);
    }

    [Fact]
    public async Task GetById_AsOtherProjectManager_ReturnsNull()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var otherPm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync($"{_marker} private", pm.Id);

        (await GetByIdAsync(project.Id, otherPm.Id, Role.ProjectManager)).ShouldBeNull();
    }

    [Fact]
    public async Task GetById_OwnerSoftDeleted_StillReturnsOwnerName()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync($"{_marker} orphaned", pm.Id);

        await using (var context = fixture.CreateDbContext())
        {
            var user = (await new UserRepository(context).GetByIdAsync(pm.Id, _ct)).ShouldNotBeNull();
            context.Entry(user).Property(u => u.IsDeleted).CurrentValue = true;
            await context.SaveChangesAsync(_ct);
        }

        var details = (await GetByIdAsync(project.Id, Guid.NewGuid(), Role.Admin)).ShouldNotBeNull();
        details.Owner.FullName.ShouldBe(pm.FullName);
        details.Owner.Email.ShouldBe(pm.Email.Value);

        var page = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);
        page.Items.ShouldHaveSingleItem().OwnerName.ShouldBe(pm.FullName);
    }

    [Fact]
    public async Task List_AsTeamMemberWithAssignedTask_ReturnsThatProject()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var assigned = await SaveProjectAsync($"{_marker} assigned", pm.Id);
        var other = await SaveProjectAsync($"{_marker} other", pm.Id);
        await SaveTaskAsync(assigned.Id, assigneeId: member.Id);
        await SaveTaskAsync(assigned.Id, status: TaskItemStatus.Completed);
        await SaveTaskAsync(other.Id);

        var page = await ListAsync(Query(search: _marker), member.Id, Role.TeamMember);

        page.TotalCount.ShouldBe(1);
        var item = page.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(assigned.Id);
        item.OwnerId.ShouldBe(pm.Id);
        item.TaskCount.ShouldBe(1);
        item.CompletedTaskCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_AsTeamMemberWhoseOnlyTaskIsDeleted_ReturnsNothing()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync($"{_marker} formerly assigned", pm.Id);
        var task = await SaveTaskAsync(project.Id, assigneeId: member.Id);
        await SoftDeleteTaskAsync(task.Id);

        var page = await ListAsync(Query(search: _marker), member.Id, Role.TeamMember);

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
        (await GetByIdAsync(project.Id, member.Id, Role.TeamMember)).ShouldBeNull();
    }

    [Fact]
    public async Task GetById_AsPmAssigneeOfAnotherPmsProject_ReturnsProject()
    {
        var owner = await SaveUserAsync(Role.ProjectManager);
        var pmAssignee = await SaveUserAsync(Role.ProjectManager);
        var project = await SaveProjectAsync($"{_marker} foreign", owner.Id);
        await SaveTaskAsync(project.Id, assigneeId: pmAssignee.Id);

        var details = (await GetByIdAsync(project.Id, pmAssignee.Id, Role.ProjectManager)).ShouldNotBeNull();

        details.Id.ShouldBe(project.Id);
        details.Owner.Id.ShouldBe(owner.Id);
        var page = await ListAsync(Query(search: _marker), pmAssignee.Id, Role.ProjectManager);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(project.Id);
    }

    [Fact]
    public async Task List_AsOwner_CountsAllNonDeletedTasks()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync($"{_marker} counted", pm.Id);
        await SaveTaskAsync(project.Id);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Completed);
        var deleted = await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Completed);
        await SoftDeleteTaskAsync(deleted.Id);

        var asOwner = await ListAsync(Query(search: _marker), pm.Id, Role.ProjectManager);
        var asAdmin = await ListAsync(Query(search: _marker), Guid.NewGuid(), Role.Admin);

        foreach (var page in new[] { asOwner, asAdmin })
        {
            var item = page.Items.ShouldHaveSingleItem();
            item.Id.ShouldBe(project.Id);
            item.TaskCount.ShouldBe(2);
            item.CompletedTaskCount.ShouldBe(1);
        }
    }

    [Fact]
    public async Task GetById_AsNonOwnerAssignee_CountsOnlyOwnTasksPerStatus()
    {
        var pm = await SaveUserAsync(Role.ProjectManager);
        var member = await SaveUserAsync(Role.TeamMember);
        var otherMember = await SaveUserAsync(Role.TeamMember);
        var project = await SaveProjectAsync($"{_marker} shared", pm.Id);
        await SaveTaskAsync(project.Id, assigneeId: member.Id);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.InProgress);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.InProgress);
        await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Cancelled);
        var deleted = await SaveTaskAsync(project.Id, assigneeId: member.Id, status: TaskItemStatus.Completed);
        await SoftDeleteTaskAsync(deleted.Id);
        await SaveTaskAsync(project.Id, assigneeId: otherMember.Id, status: TaskItemStatus.Completed);
        await SaveTaskAsync(project.Id, status: TaskItemStatus.ToDo);

        var asAssignee = (await GetByIdAsync(project.Id, member.Id, Role.TeamMember)).ShouldNotBeNull();
        var asOwner = (await GetByIdAsync(project.Id, pm.Id, Role.ProjectManager)).ShouldNotBeNull();
        var listed = (await ListAsync(Query(search: _marker), member.Id, Role.TeamMember)).Items.ShouldHaveSingleItem();

        asAssignee.TaskCounts.ShouldBe(new ProjectTaskCountsDto(ToDo: 1, InProgress: 2, Completed: 0, Cancelled: 1));
        asOwner.TaskCounts.ShouldBe(new ProjectTaskCountsDto(ToDo: 2, InProgress: 2, Completed: 1, Cancelled: 1));
        listed.TaskCount.ShouldBe(4);
        listed.CompletedTaskCount.ShouldBe(0);
    }

    private static ListProjectsQuery Query(
        string? search = null,
        Guid? ownerId = null,
        string? sortBy = null,
        SortDirection direction = SortDirection.Ascending,
        int page = 1,
        int pageSize = 20) =>
        new(search, ownerId, sortBy, direction, page, pageSize);

    private async Task<PagedResult<ProjectListItemDto>> ListAsync(ListProjectsQuery query, Guid callerId, Role role)
    {
        await using var context = fixture.CreateDbContext();
        return await new ProjectReadRepository(context).ListAsync(query, callerId, role, _ct);
    }

    private async Task<ProjectDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role role)
    {
        await using var context = fixture.CreateDbContext();
        return await new ProjectReadRepository(context).GetByIdAsync(id, callerId, role, _ct);
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

    private async Task<Project> SaveProjectAsync(string name, Guid ownerId, string? description = null)
    {
        var project = Project.Create(name, description, ownerId);
        await using var context = fixture.CreateDbContext();
        new ProjectRepository(context).Add(project);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return project;
    }

    private async Task UpdateProjectAsync(Guid id)
    {
        await using var context = fixture.CreateDbContext();
        var project = (await new ProjectRepository(context).GetByIdAsync(id, _ct)).ShouldNotBeNull();
        project.UpdateDetails(project.Name, "updated " + Guid.NewGuid().ToString("N"));
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }

    private async Task<TaskItem> SaveTaskAsync(
        Guid projectId, Guid? assigneeId = null, TaskItemStatus status = TaskItemStatus.ToDo)
    {
        var task = TaskItem.Create(projectId, $"{_marker} task", null, TaskPriority.Medium, null, assigneeId);
        task.ChangeStatus(status);
        await using var context = fixture.CreateDbContext();
        new TaskItemRepository(context).Add(task);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return task;
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
