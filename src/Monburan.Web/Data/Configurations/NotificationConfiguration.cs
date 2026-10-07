using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
     .HasConversion(
         id => id.Value,
         value => Id.Create(value)
     )
     .HasColumnType("uuid");
        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.RecipientId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Type)
            .HasMaxLength(50);

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.Property(n => n.ReadAt)
            .IsRequired(false);

        builder.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt })
            .HasDatabaseName("IX_Notifications_RecipientId_IsRead_CreatedAt");

        builder.HasIndex(n => new { n.RecipientId, n.Type })
            .HasDatabaseName("IX_Notifications_RecipientId_Type");
    }
}