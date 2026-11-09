using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence;

/// <summary>Saves the DbContext; a stale RowVersion becomes an expected Conflict.</summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public const string ConcurrencyConflictCode = "concurrency.conflict";

    public const string DuplicateKeyCode = PersistenceErrors.DuplicateKeyCode;

    // SQL Server error numbers: 2601 = duplicate key in a unique index, 2627 = unique/primary key constraint violation.
    private const int DuplicateIndexNumber = 2601;
    private const int DuplicateKeyNumber = 2627;

    private static readonly Error ConcurrencyConflict = Error.Conflict(
        ConcurrencyConflictCode,
        "The resource was changed by another request. Reload it and try again.");

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: DuplicateIndexNumber or DuplicateKeyNumber })
        {
            return Result.Failure(PersistenceErrors.DuplicateKey);
        }
    }
}
