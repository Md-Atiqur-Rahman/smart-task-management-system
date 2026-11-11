using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    private const int EnumMaxLength = 20;

    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).HasMaxLength(TaskItem.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(TaskItem.DescriptionMaxLength);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(EnumMaxLength).IsRequired();
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(EnumMaxLength).IsRequired();

        // DateOnly maps to the SQL Server date type natively (EF Core 8+), no converter.
        builder.Property(t => t.DueDate);

        builder.Property(t => t.ProjectId).IsRequired();
        builder.HasIndex(t => t.ProjectId);
        builder.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.AssigneeId);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureAuditable<TaskItem, Guid>();
    }
}
