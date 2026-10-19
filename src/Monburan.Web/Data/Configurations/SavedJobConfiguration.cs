using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class SavedJobConfiguration : IEntityTypeConfiguration<SavedJob>
{
    public void Configure(EntityTypeBuilder<SavedJob> builder)
    {
        builder.ToTable("SavedJobs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(s => s.CandidateId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(s => s.JobId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Job)
            .WithMany()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.CandidateId, s.JobId })
            .IsUnique()
            .HasDatabaseName("IX_SavedJobs_CandidateId_JobId");

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_SavedJobs_CreatedAt");
    }
}
