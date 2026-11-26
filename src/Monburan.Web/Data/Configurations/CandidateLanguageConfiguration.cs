using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class CandidateLanguageConfiguration : IEntityTypeConfiguration<CandidateLanguage>
{
    public void Configure(EntityTypeBuilder<CandidateLanguage> builder)
    {
        builder.ToTable("CandidateLanguages");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.CandidateId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.LanguageTag)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Level)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.Evidence)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(p => p.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.CandidateId, p.LanguageTag })
            .IsUnique()
            .HasDatabaseName("UX_CandidateLanguages_Candidate_Language");
    }
}
