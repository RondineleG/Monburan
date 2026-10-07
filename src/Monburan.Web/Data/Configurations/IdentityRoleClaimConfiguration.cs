using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monburan.Web.Data.Configurations;

public class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("RoleClaims", "Identity");

        builder.Property(rc => rc.ClaimType)
            .HasMaxLength(200);

        builder.Property(rc => rc.ClaimValue)
            .HasMaxLength(500);

        builder.HasIndex(rc => rc.RoleId)
            .HasDatabaseName("IX_RoleClaims_RoleId");
    }
}