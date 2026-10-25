using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class ResumeShareConfiguration : IEntityTypeConfiguration<ResumeShare>
{
    public void Configure(EntityTypeBuilder<ResumeShare> builder)
    {
        builder.ToTable("ResumeShares");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(s => s.ResumeId)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(s => s.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(s => s.Label).HasMaxLength(100);

        builder.HasOne(s => s.Resume)
            .WithMany()
            .HasForeignKey(s => s.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_ResumeShares_TokenHash");

        builder.HasIndex(s => s.ResumeId)
            .HasDatabaseName("IX_ResumeShares_ResumeId");
    }
}
