using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;

namespace Monburan.Web.Data.Configurations;

public class IntegrationTokenConfiguration : IEntityTypeConfiguration<IntegrationToken>
{
    public void Configure(EntityTypeBuilder<IntegrationToken> builder)
    {
        builder.ToTable("IntegrationTokens");

        builder.Property(token => token.Name).IsRequired().HasMaxLength(80);
        builder.Property(token => token.TokenHash).IsRequired().HasMaxLength(64).IsUnicode(false);
        builder.Property(token => token.Scopes).IsRequired().HasMaxLength(200).IsUnicode(false);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_IntegrationTokens_TokenHash");

        builder.HasIndex(token => new { token.UserId, token.RevokedAt })
            .HasDatabaseName("IX_IntegrationTokens_UserId_RevokedAt");

        builder.HasIndex(token => token.CandidateId)
            .HasDatabaseName("IX_IntegrationTokens_CandidateId");
    }
}
