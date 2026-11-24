using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class IntegrationTokenConfiguration : IEntityTypeConfiguration<IntegrationToken>
{
    public void Configure(EntityTypeBuilder<IntegrationToken> builder)
    {
        builder.ToTable("IntegrationTokens");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Prefix).IsRequired().HasMaxLength(16);
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Scopes).IsRequired().HasMaxLength(500);

        builder.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("IX_IntegrationTokens_TokenHash");
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_IntegrationTokens_UserId");

        builder.Ignore(e => e.ScopeList);
        builder.Ignore(e => e.IsActive);
    }
}
