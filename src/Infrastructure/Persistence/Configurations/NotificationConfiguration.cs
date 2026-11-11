using Datavanced.SmartTaskManagement.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Configurations;

/// <summary>Not auditable on purpose: no soft-delete filter and no RowVersion (design 4).</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public const string EmailClaimTokenProperty = "EmailClaimToken";

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.RecipientId).IsRequired();
        builder.Property(n => n.Type).HasConversion<int>().IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(Notification.SubjectMaxLength).IsRequired();
        builder.Property(n => n.ActorName).HasMaxLength(Notification.ActorNameMaxLength).IsRequired();
        builder.Property(n => n.EmailStatus).HasConversion<int>().IsRequired();
        builder.Property(n => n.EmailLastError).HasMaxLength(Notification.EmailErrorMaxLength);

        // Shadow property: written only by the outbox claim (email worker).
        builder.Property<Guid?>(EmailClaimTokenProperty);

        builder.HasIndex(n => new { n.RecipientId, n.CreatedAt });
        builder.HasIndex(n => new { n.EmailStatus, n.EmailNextAttemptAt });

        builder.Ignore(n => n.IsRead);
    }
}
