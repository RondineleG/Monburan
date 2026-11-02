using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class JobSubmissionEvidenceConfiguration : IEntityTypeConfiguration<JobSubmissionEvidence>
{
    public void Configure(EntityTypeBuilder<JobSubmissionEvidence> builder)
    {
        builder.ToTable("JobSubmissionEvidences");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(e => e.SubmissionId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(e => e.StorageKey).IsRequired().HasMaxLength(300);

        builder.HasOne(e => e.Submission)
            .WithMany(s => s.Evidences)
            .HasForeignKey(e => e.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Sha256)
            .HasDatabaseName("IX_JobSubmissionEvidences_Sha256");
    }
}
