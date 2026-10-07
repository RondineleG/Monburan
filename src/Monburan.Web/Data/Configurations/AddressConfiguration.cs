using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
     .HasConversion(
         id => id.Value,
         value => Id.Create(value)
     )
     .HasColumnType("uuid");

        builder.Property(a => a.City).HasMaxLength(100);
        builder.Property(a => a.Complement).HasMaxLength(100);
        builder.Property(a => a.Country).HasMaxLength(5).HasDefaultValue("BR").IsFixedLength();
        builder.Property(a => a.District).HasMaxLength(100);
        builder.Property(a => a.Number).HasMaxLength(20);
        builder.Property(a => a.State).HasMaxLength(5);
        builder.Property(a => a.Street).HasMaxLength(200);
        builder.Property(a => a.ZipCode).HasMaxLength(20);
    }
}