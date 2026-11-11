using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const int RoleMaxLength = 20;
    private const int PasswordHashMaxLength = 500;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).HasMaxLength(User.FullNameMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(PasswordHashMaxLength).IsRequired();
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(RoleMaxLength)
            .IsRequired();
        builder.Property(u => u.TokenVersion).IsRequired();

        builder.ConfigureAuditable<User, Guid>();
    }
}
