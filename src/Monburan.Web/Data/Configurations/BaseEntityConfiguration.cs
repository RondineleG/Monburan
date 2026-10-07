using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Converters;
using Monburan.Core.Entities.Base;

namespace Monburan.Web.Data.Configurations;

public class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(x => x.Id)
           .HasConversion(new IdValueConverter(), new IdValueComparer())
            .HasColumnType("uuid")
           .ValueGeneratedNever();

        builder.Property(e => e.Id)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(100);

        builder.Property(e => e.DeletedAt)
            .IsRequired(false);

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(100);

        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(e => new { e.TenantId, e.Id }).IsUnique();
    }
}