
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Authoring.Infrastructure.Persistence.Entities;

namespace Platform.Authoring.Infrastructure.Persistence.Configurations;

public sealed class LaboratoryConfiguration
    : IEntityTypeConfiguration<LaboratoryRecord>
{
    public void Configure(EntityTypeBuilder<LaboratoryRecord> builder)
    {
        builder.ToTable("laboratories");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedNever();

        builder.Property(l => l.Title)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(l => l.Goal)
            .HasMaxLength(2000);

        builder.Property(l => l.Difficulty)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(l => l.Type)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.Tags)
            .HasColumnType("text[]")
            .IsRequired();
    }
}
