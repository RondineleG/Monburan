using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class JobRevisionConfiguration : IEntityTypeConfiguration<JobRevision>
{
    public void Configure(EntityTypeBuilder<JobRevision> builder)
    {
        builder.ToTable("JobRevisions");

        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(revision => revision.JobId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(revision => revision.Action)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(revision => revision.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(revision => revision.ActorId)
            .HasMaxLength(100);

        builder.Property(revision => revision.SnapshotJson)
            .HasColumnType("jsonb");

        builder.HasOne(revision => revision.Job)
            .WithMany()
            .HasForeignKey(revision => revision.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(revision => new { revision.JobId, revision.Revision })
            .IsUnique()
            .HasDatabaseName("UX_JobRevisions_JobId_Revision");
    }
}
