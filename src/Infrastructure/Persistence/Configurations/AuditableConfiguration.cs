using Contoso.TemplateApp.Domain.Primitives;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Contoso.TemplateApp.Infrastructure.Persistence.Configurations;

/// <summary>Shared mapping for every auditable aggregate: audit fields, soft delete filter, RowVersion.</summary>
internal static class AuditableConfiguration
{
    public static void ConfigureAuditable<TEntity, TId>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableAggregateRoot<TId>
        where TId : notnull
    {
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.IsDeleted).IsRequired();
        builder.Property(e => e.DeletedAt);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
