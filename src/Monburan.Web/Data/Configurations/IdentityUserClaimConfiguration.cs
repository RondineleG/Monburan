using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monburan.Web.Data.Configurations;

public class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("UserClaims", "Identity");

        builder.Property(c => c.ClaimType)
            .HasMaxLength(200);

        builder.Property(c => c.ClaimValue)
            .HasMaxLength(500);

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("IX_UserClaims_UserId");
    }
}