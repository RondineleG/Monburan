using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monburan.Web.Data.Configurations;

public class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("UserLogins", "Identity");

        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });

        builder.Property(l => l.LoginProvider)
            .HasMaxLength(200);

        builder.Property(l => l.ProviderKey)
            .HasMaxLength(200);

        builder.Property(l => l.ProviderDisplayName)
            .HasMaxLength(200);

        builder.HasIndex(l => l.UserId)
            .HasDatabaseName("IX_UserLogins_UserId");
    }
}