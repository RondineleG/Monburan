using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
     .HasConversion(
         id => id.Value,
         value => Id.Create(value)
     )
     .HasColumnType("uuid");
        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Notes)
            .HasMaxLength(1000);

        builder.Property(a => a.AppliedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(a => a.Candidate)
            .WithMany(c => c.Applications)
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Job)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.Status, a.CandidateId })
            .HasDatabaseName("IX_Applications_Status_CandidateId");

        builder.HasIndex(a => new { a.JobId, a.Status })
            .HasDatabaseName("IX_Applications_JobId_Status");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("IX_Applications_CreatedAt");

        builder.HasIndex(a => new { a.CandidateId, a.JobId })
            .IsUnique()
            .HasDatabaseName("IX_Applications_CandidateId_JobId");
    }
}