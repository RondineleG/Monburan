using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class JobSubmissionConfiguration : IEntityTypeConfiguration<JobSubmission>
{
    public void Configure(EntityTypeBuilder<JobSubmission> builder)
    {
        builder.ToTable("JobSubmissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(s => s.Title).IsRequired().HasMaxLength(200);
        builder.Property(s => s.CompanyName).HasMaxLength(200);
        builder.Property(s => s.City).HasMaxLength(200);
        builder.Property(s => s.State).HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.SourceUrl).HasMaxLength(500);
        builder.Property(s => s.SourceDescription).HasMaxLength(500);
        builder.Property(s => s.ContactEmail).HasMaxLength(200);
        builder.Property(s => s.ModerationNote).HasMaxLength(2000);
        builder.Property(s => s.ModeratedBy).HasMaxLength(100);
        builder.Property(s => s.SubmitterKeyHash).HasMaxLength(64);

        // Referências opcionais guardadas como uuid nulo
        builder.Property(s => s.ApprovedJobId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : Id.Create(value.Value))
            .HasColumnType("uuid");
        builder.Property(s => s.DuplicateOfJobId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : Id.Create(value.Value))
            .HasColumnType("uuid");
        builder.Property(s => s.DuplicateOfSubmissionId)
            .HasConversion(id => id == null ? (Guid?)null : id.Value, value => value == null ? null : Id.Create(value.Value))
            .HasColumnType("uuid");

        builder.Property(s => s.AiDraftJson).HasColumnType("text");
        builder.Property(s => s.AiDraftEvidenceHash).HasMaxLength(64);
        builder.Property(s => s.AiDraftProvider).HasMaxLength(200);

        builder.HasIndex(s => new { s.Status, s.CreatedAt })
            .HasDatabaseName("IX_JobSubmissions_Status_CreatedAt");

        builder.HasIndex(s => s.SourceUrl)
            .HasDatabaseName("IX_JobSubmissions_SourceUrl");
    }
}
