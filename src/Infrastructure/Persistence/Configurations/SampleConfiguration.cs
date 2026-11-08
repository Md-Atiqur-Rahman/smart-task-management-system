// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Domain.Samples;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Contoso.TemplateApp.Infrastructure.Persistence.Configurations;

public sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    private const int StatusMaxLength = 20;

    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("Samples");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(Sample.NameMaxLength).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(Sample.DescriptionMaxLength);
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(StatusMaxLength)
            .IsRequired();

        builder.ConfigureAuditable<Sample, Guid>();
    }
}
