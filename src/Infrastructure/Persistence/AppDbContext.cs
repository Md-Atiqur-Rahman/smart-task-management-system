using Microsoft.EntityFrameworkCore;

namespace Contoso.TemplateApp.Infrastructure.Persistence;

/// <summary>The only DbContext. Lives in Infrastructure; Application and Domain never see it.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
