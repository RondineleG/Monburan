using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class JobSubmissionHistoryConfiguration : IEntityTypeConfiguration<JobSubmissionHistory>
{
    public void Configure(EntityTypeBuilder<JobSubmissionHistory> builder)
    {
        builder.ToTable("JobSubmissionHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(h => h.SubmissionId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(h => h.ActorId).HasMaxLength(100);
        builder.Property(h => h.Note).HasMaxLength(2000);

        builder.HasOne(h => h.Submission)
            .WithMany(s => s.History)
            .HasForeignKey(h => h.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
