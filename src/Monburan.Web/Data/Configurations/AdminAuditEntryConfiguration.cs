using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class AdminAuditEntryConfiguration : IEntityTypeConfiguration<AdminAuditEntry>
{
    public void Configure(EntityTypeBuilder<AdminAuditEntry> builder)
    {
        builder.ToTable("AdminAuditEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(e => e.Action).IsRequired().HasMaxLength(60);
        builder.Property(e => e.ActorId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ActorEmail).IsRequired().HasMaxLength(256);
        builder.Property(e => e.TargetType).IsRequired().HasMaxLength(40);
        builder.Property(e => e.TargetId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.TargetLabel).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Details).HasMaxLength(1000);

        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_AdminAuditEntries_CreatedAt");
        builder.HasIndex(e => new { e.TargetType, e.TargetId }).HasDatabaseName("IX_AdminAuditEntries_Target");
    }
}
