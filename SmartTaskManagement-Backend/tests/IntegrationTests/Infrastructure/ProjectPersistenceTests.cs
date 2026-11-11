using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Project persistence against a real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class ProjectPersistenceTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddProject_ThenGetById_ReturnsProjectWithAuditFields()
    {
        var owner = await SaveOwnerAsync();
        var project = Project.Create("Roadmap", "Q1 plan", owner.Id);
        await SaveProjectAsync(project);

        await using var context = fixture.CreateDbContext();
        var loaded = await new ProjectRepository(context).GetByIdAsync(project.Id, _ct);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Roadmap");
        loaded.Description.ShouldBe("Q1 plan");
        loaded.OwnerId.ShouldBe(owner.Id);
        loaded.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task GetById_SoftDeletedProject_ReturnsNull()
    {
        var owner = await SaveOwnerAsync();
        var project = Project.Create("Gone", null, owner.Id);
        await SaveProjectAsync(project);

        await using (var context = fixture.CreateDbContext())
        {
            var loaded = (await new ProjectRepository(context).GetByIdAsync(project.Id, _ct)).ShouldNotBeNull();
            loaded.Delete(fixture.Time.GetUtcNow());
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var verify = fixture.CreateDbContext();
        (await new ProjectRepository(verify).GetByIdAsync(project.Id, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task AnyOwnedBy_OnlyDeletedProjects_ReturnsFalse()
    {
        var owner = await SaveOwnerAsync();
        var other = await SaveOwnerAsync();
        var project = Project.Create("Soon deleted", null, owner.Id);
        await SaveProjectAsync(project);

        await using (var context = fixture.CreateDbContext())
        {
            var loaded = (await new ProjectRepository(context).GetByIdAsync(project.Id, _ct)).ShouldNotBeNull();
            loaded.Delete(fixture.Time.GetUtcNow());
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using (var context = fixture.CreateDbContext())
        {
            (await new ProjectRepository(context).AnyOwnedByAsync(owner.Id, _ct)).ShouldBeFalse();
        }

        await SaveProjectAsync(Project.Create("Live", null, owner.Id));

        await using var verify = fixture.CreateDbContext();
        var repository = new ProjectRepository(verify);
        (await repository.AnyOwnedByAsync(owner.Id, _ct)).ShouldBeTrue();
        (await repository.AnyOwnedByAsync(other.Id, _ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task AddProject_UnknownOwner_ThrowsDbUpdateException()
    {
        await using var context = fixture.CreateDbContext();
        new ProjectRepository(context).Add(Project.Create("Orphan", null, Guid.NewGuid()));

        await Should.ThrowAsync<DbUpdateException>(() => new UnitOfWork(context).SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task UpdateSameProject_FromTwoContexts_SecondSaveIsConcurrencyConflict()
    {
        var owner = await SaveOwnerAsync();
        var project = Project.Create("Shared", null, owner.Id);
        await SaveProjectAsync(project);

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var firstCopy = await new ProjectRepository(first).GetByIdAsync(project.Id, _ct);
        var secondCopy = await new ProjectRepository(second).GetByIdAsync(project.Id, _ct);

        firstCopy.ShouldNotBeNull().UpdateDetails("First", null);
        secondCopy.ShouldNotBeNull().UpdateDetails("Second", null);

        (await new UnitOfWork(first).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        var result = await new UnitOfWork(second).SaveChangesAsync(_ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("concurrency.conflict");
    }

    private async Task<User> SaveOwnerAsync()
    {
        var user = User.Register(Email.Create($"owner.{Guid.NewGuid():N}@example.com"), "Owner", "hash");
        user.ChangeRole(Role.ProjectManager);
        await using var context = fixture.CreateDbContext();
        new UserRepository(context).Add(user);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        return user;
    }

    private async Task SaveProjectAsync(Project project)
    {
        await using var context = fixture.CreateDbContext();
        new ProjectRepository(context).Add(project);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }
}
