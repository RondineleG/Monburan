using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class JobAlertConfiguration : IEntityTypeConfiguration<JobAlert>
{
    public void Configure(EntityTypeBuilder<JobAlert> builder)
    {
        builder.ToTable("JobAlerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(a => a.CandidateId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(a => a.Keywords)
            .HasMaxLength(200);

        builder.Property(a => a.City)
            .HasMaxLength(100);

        builder.Property(a => a.State)
            .HasMaxLength(2);

        builder.Property(a => a.Mode)
            .HasMaxLength(20);

        builder.Property(a => a.Category)
            .HasMaxLength(100);

        builder.Property(a => a.SalaryMin)
            .HasColumnType("decimal(18,2)");

        builder.Property(a => a.IsActive)
            .HasDefaultValue(true);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.IsActive)
            .HasDatabaseName("IX_JobAlerts_IsActive");

        builder.HasIndex(a => a.CandidateId)
            .HasDatabaseName("IX_JobAlerts_CandidateId");
    }
}
