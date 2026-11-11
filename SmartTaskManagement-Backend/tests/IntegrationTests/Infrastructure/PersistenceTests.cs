// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Persistence base against a real SQL Server: audit fields, soft-delete filter, optimistic concurrency.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class PersistenceTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SavedSample_LoadsFromSqlServer_WithAuditFields()
    {
        var sample = Sample.Create("persisted", "loaded back through EF and NullGuard");
        await SaveNewAsync(sample);

        await using var context = fixture.CreateDbContext();
        var loaded = await new SampleRepository(context).GetByIdAsync(sample.Id, _ct);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("persisted");
        loaded.Description.ShouldBe("loaded back through EF and NullGuard");
        loaded.Status.ShouldBe(SampleStatus.Open);
        loaded.CreatedAt.ShouldBe(fixture.Time.GetUtcNow());
        loaded.UpdatedAt.ShouldBeNull();
        loaded.RowVersion.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SampleWithNullDescription_LoadsFromSqlServer()
    {
        var sample = Sample.Create("no description", null);
        await SaveNewAsync(sample);

        await using var context = fixture.CreateDbContext();
        var loaded = await new SampleRepository(context).GetByIdAsync(sample.Id, _ct);

        loaded.ShouldNotBeNull();
        loaded.Description.ShouldBeNull();
    }

    [Fact]
    public async Task MissingSample_ReturnsNull()
    {
        await using var context = fixture.CreateDbContext();

        var loaded = await new SampleRepository(context).GetByIdAsync(Guid.NewGuid(), _ct);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task ModifiedSample_GetsUpdatedAt()
    {
        var sample = Sample.Create("to complete", null);
        await SaveNewAsync(sample);

        await using (var context = fixture.CreateDbContext())
        {
            var loaded = await new SampleRepository(context).GetByIdAsync(sample.Id, _ct);
            loaded.ShouldNotBeNull().Complete().IsSuccess.ShouldBeTrue();
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var verify = fixture.CreateDbContext();
        var reloaded = await new SampleRepository(verify).GetByIdAsync(sample.Id, _ct);
        reloaded.ShouldNotBeNull().Status.ShouldBe(SampleStatus.Completed);
        reloaded.UpdatedAt.ShouldBe(fixture.Time.GetUtcNow());
    }

    [Fact]
    public async Task DeletedSample_IsHiddenByGlobalFilter()
    {
        var sample = Sample.Create("to delete", null);
        await SaveNewAsync(sample);

        await using (var context = fixture.CreateDbContext())
        {
            var loaded = await new SampleRepository(context).GetByIdAsync(sample.Id, _ct);
            loaded.ShouldNotBeNull().Delete(fixture.Time.GetUtcNow());
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var verify = fixture.CreateDbContext();
        (await new SampleRepository(verify).GetByIdAsync(sample.Id, _ct)).ShouldBeNull();
        var raw = await verify.Set<Sample>().IgnoreQueryFilters()
            .SingleAsync(s => s.Id == sample.Id, _ct);
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedAt.ShouldBe(fixture.Time.GetUtcNow());
    }

    // Review Focus 1: a stale RowVersion returns Conflict, not an exception.
    [Fact]
    public async Task StaleRowVersion_SaveChanges_ReturnsConflict()
    {
        var sample = Sample.Create("contended", null);
        await SaveNewAsync(sample);

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        var firstCopy = await new SampleRepository(first).GetByIdAsync(sample.Id, _ct);
        var secondCopy = await new SampleRepository(second).GetByIdAsync(sample.Id, _ct);

        firstCopy.ShouldNotBeNull().Complete();
        (await new UnitOfWork(first).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();

        secondCopy.ShouldNotBeNull().Delete(fixture.Time.GetUtcNow());
        var result = await new UnitOfWork(second).SaveChangesAsync(_ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe(UnitOfWork.ConcurrencyConflictCode);
    }

    private async Task SaveNewAsync(Sample sample)
    {
        await using var context = fixture.CreateDbContext();
        new SampleRepository(context).Add(sample);
        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }
}
