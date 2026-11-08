using Contoso.TemplateApp.Domain.Abstractions;
using Contoso.TemplateApp.Domain.Results;

using Microsoft.EntityFrameworkCore;

namespace Contoso.TemplateApp.Infrastructure.Persistence;

/// <summary>Saves the DbContext; a stale RowVersion becomes an expected Conflict.</summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public const string ConcurrencyConflictCode = "concurrency.conflict";

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
    }
}
