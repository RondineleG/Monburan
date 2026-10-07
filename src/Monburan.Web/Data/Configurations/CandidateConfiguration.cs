using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("Candidates");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
  .HasConversion(
      id => id.Value,
      value => Id.Create(value)
  )
  .HasColumnType("uuid");
        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Phone)
            .HasMaxLength(25);

        builder.Property(c => c.DateOfBirth)
            .HasColumnType("date");

        builder.HasOne(c => c.Address)
            .WithOne()
            .HasForeignKey<Candidate>(c => c.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasDatabaseName("IX_Candidates_Email");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("IX_Candidates_Status");

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("IX_Candidates_Name");

        builder.HasIndex(c => new { c.Status, c.CreatedAt })
            .HasDatabaseName("IX_Candidates_Status_CreatedAt");
    }
}