using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monburan.Web.Data.Configurations;

public class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("UserTokens", "Identity");

        builder.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });

        builder.Property(t => t.LoginProvider)
            .HasMaxLength(200);

        builder.Property(t => t.Name)
            .HasMaxLength(200);

        builder.Property(t => t.Value)
            .HasMaxLength(2000);
    }
}