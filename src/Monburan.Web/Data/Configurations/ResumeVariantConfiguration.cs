using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class ResumeVariantConfiguration : IEntityTypeConfiguration<ResumeVariant>
{
    public void Configure(EntityTypeBuilder<ResumeVariant> builder)
    {
        builder.ToTable("ResumeVariants");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.ResumeId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.CandidateId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.Locale)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.OverridesJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.PendingSuggestionsJson)
            .HasColumnType("jsonb");

        builder.HasOne<Resume>()
            .WithMany()
            .HasForeignKey(p => p.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma variante por idioma entre as não excluídas: excluir es-ES e criar de novo precisa funcionar
        builder.HasIndex(p => new { p.ResumeId, p.Locale })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_ResumeVariants_Resume_Locale");

        builder.HasIndex(p => p.CandidateId)
            .HasDatabaseName("IX_ResumeVariants_CandidateId");
    }
}
