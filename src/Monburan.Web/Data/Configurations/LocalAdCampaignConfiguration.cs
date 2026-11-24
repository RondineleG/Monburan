using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monburan.Core.Entities;

namespace Monburan.Web.Data.Configurations;

public sealed class LocalAdCampaignConfiguration : IEntityTypeConfiguration<LocalAdCampaign>
{
    public void Configure(EntityTypeBuilder<LocalAdCampaign> builder)
    {
        builder.ToTable("LocalAdCampaigns");
        builder.Property(campaign => campaign.SponsorName).IsRequired().HasMaxLength(120);
        builder.Property(campaign => campaign.Headline).IsRequired().HasMaxLength(100);
        builder.Property(campaign => campaign.Description).HasMaxLength(280);
        builder.Property(campaign => campaign.ImageUrl).HasMaxLength(1000);
        builder.Property(campaign => campaign.TargetUrl).IsRequired().HasMaxLength(1000);
        builder.Property(campaign => campaign.TargetLocationsJson).IsRequired().HasMaxLength(4000);
        builder.Property(campaign => campaign.TargetStates).IsRequired().HasMaxLength(10);
        builder.Property(campaign => campaign.StartsAt).IsRequired();
        builder.Property(campaign => campaign.EndsAt).IsRequired();
        builder.Property(campaign => campaign.IsActive).HasDefaultValue(false);
        builder.Property(campaign => campaign.ImpressionCount).HasDefaultValue(0);
        builder.Property(campaign => campaign.ClickCount).HasDefaultValue(0);
        builder.HasIndex(campaign => new { campaign.IsActive, campaign.StartsAt, campaign.EndsAt })
            .HasDatabaseName("IX_LocalAdCampaigns_Active_Period");
    }
}
