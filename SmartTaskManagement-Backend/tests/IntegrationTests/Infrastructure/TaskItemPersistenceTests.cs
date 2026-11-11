using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Task persistence against a real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class TaskItemPersistenceTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddTask_ThenGetById_RoundTripsAllFieldsIncludingDueDate()
    {
        var owner = await SaveUserAsync();
        var project = await SaveProjectAsync(owner.Id);
        var assignee = await SaveUserAsync();
        var task = TaskItem.Create(project.Id, "Write docs", "Cover the API", TaskPriority.High, new DateOnly(2026, 11, 20), assignee.Id);
        task.ChangeStatus(TaskItemStatus.InProgress);
        await SaveTaskAsync(task);

        await using var context = fixture.CreateDbContext();
        var loaded = await new TaskItemRepository(context).GetByIdAsync(task.Id, _ct);

        loaded.ShouldNotBeNull();
        loaded.ProjectId.ShouldBe(project.Id);
        loaded.Title.ShouldBe("Write docs");
        loaded.Description.ShouldBe("Cover the API");
        loaded.Status.ShouldBe(TaskItemStatus.InProgress);
        loaded.Priority.ShouldBe(TaskPriority.High);
        loaded.DueDate.ShouldBe(new DateOnly(2026, 11, 20));
        loaded.AssigneeId.ShouldBe(assignee.Id);
        loaded.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task GetById_SoftDeletedTask_ReturnsNull()
    {
        var owner = await SaveUserAsync();
        var project = await SaveProjectAsync(owner.Id);
        var task = TaskItem.Create(project.Id, "Gone", null, TaskPriority.Low, null, null);
        await SaveTaskAsync(task);

        await using (var context = fixture.CreateDbContext())
        {
            var loaded = (await new TaskItemRepository(context).GetByIdAsync(task.Id, _ct)).ShouldNotBeNull();
            loaded.Delete(fixture.Time.GetUtcNow());
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var verify = fixture.CreateDbContext();
        (await new TaskItemRepository(verify).GetByIdAsync(task.Id, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task AddTask_UnknownProject_ThrowsDbUpdateException()
    {
        await using var context = fixture.CreateDbContext();
        new TaskItemRepository(context).Add(TaskItem.Create(Guid.NewGuid(), "Orphan", null, TaskPriority.Low, null, null));

        await Should.ThrowAsync<DbUpdateException>(() => new UnitOfWork(context).SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task AddTask_UnknownAssignee_ThrowsDbUpdateException()
    {
        var owner = await SaveUserAsync();
        var project = await SaveProjectAsync(owner.Id);

        await using var context = fixture.CreateDbContext();
        new TaskItemRepository(context).Add(TaskItem.Create(project.Id, "Nobody", null, TaskPriority.Low, null, Guid.NewGuid()));

        await Should.ThrowAsync<DbUpdateException>(() => new UnitOfWork(context).SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task UpdateSameTask_FromTwoContexts_SecondSaveIsConcurrencyConflict()
    {
        var owner = await SaveUserAsync();
        var project = await SaveProjectAsync(owner.Id);
        var task = TaskItem.Create(project.Id, "Shared", null, TaskPriority.Medium, null, null);
        await SaveTaskAsync(task);

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var firstCopy = await new TaskItemRepository(first).GetByIdAsync(task.Id, _ct);
        var secondCopy = await new TaskItemRepository(second).GetByIdAsync(task.Id, _ct);

        firstCopy.ShouldNotBeNull().UpdateDetails("First", null, TaskPriority.Low, null);
        secondCopy.ShouldNotBeNull().UpdateDetails("Second", null, TaskPriority.High, null);

        (await new UnitOfWork(first).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        var result = await new UnitOfWork(second).SaveChangesAsync(_ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("concurrency.conflict");
    }

    private async Task<User> SaveUserAsync()
    {
        var user = User.Register(Email.Create($"task.{Guid.NewGuid():N}@example.com"), "Member", "hash");
        user.ChangeRole(Role.ProjectManager);
        await using var context = fixture.CreateDbContext();
        new UserRepository(context).Add(user);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return user;
    }

    private async Task<Project> SaveProjectAsync(Guid ownerId)
    {
        var project = Project.Create($"Project {Guid.NewGuid():N}", null, ownerId);
        await using var context = fixture.CreateDbContext();
        new ProjectRepository(context).Add(project);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return project;
    }

    private async Task SaveTaskAsync(TaskItem task)
    {
        await using var context = fixture.CreateDbContext();
        new TaskItemRepository(context).Add(task);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }
}
