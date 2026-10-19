using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class CareerProfileConfiguration : IEntityTypeConfiguration<CareerProfile>
{
    public void Configure(EntityTypeBuilder<CareerProfile> builder)
    {
        builder.ToTable("CareerProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.CandidateId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.DesiredRole)
            .HasMaxLength(120);

        builder.Property(p => p.City)
            .HasMaxLength(100);

        builder.Property(p => p.State)
            .HasMaxLength(2);

        builder.Property(p => p.Mode)
            .HasMaxLength(20);

        builder.Property(p => p.Category)
            .HasMaxLength(100);

        builder.Property(p => p.MinimumSalary)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.ContractType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(p => p.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.CandidateId)
            .IsUnique()
            .HasDatabaseName("IX_CareerProfiles_CandidateId");
    }
}
