using Contoso.TemplateApp.Domain.Primitives;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Contoso.TemplateApp.Infrastructure.Persistence.Interceptors;

/// <summary>Sets CreatedAt / UpdatedAt from TimeProvider (never DateTime.UtcNow).</summary>
public sealed class AuditInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string CreatedAt = nameof(AuditableAggregateRoot<Guid>.CreatedAt);
    private const string UpdatedAt = nameof(AuditableAggregateRoot<Guid>.UpdatedAt);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Stamp(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(CreatedAt) is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(CreatedAt).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(UpdatedAt).CurrentValue = now;
            }
        }
    }
}
