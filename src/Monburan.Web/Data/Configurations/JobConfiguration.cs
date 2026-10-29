using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
     .HasConversion(
         id => id.Value,
         value => Id.Create(value)
     )
     .HasColumnType("uuid");
        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.ContractType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(j => j.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(j => j.Requirements)
            .HasMaxLength(2000);

        builder.Property(j => j.Responsibilities)
            .HasMaxLength(2000);

        builder.Property(j => j.Benefits)
            .HasMaxLength(2000);

        builder.Property(j => j.ApplicationInstructions)
            .HasMaxLength(2000);

        builder.Property(j => j.NumberOfOpenings)
            .HasDefaultValue(1);

        builder.Property(j => j.Observations)
            .HasMaxLength(2000);

        builder.Property(j => j.Category)
            .HasMaxLength(100);

        builder.Property(j => j.Tags)
            .HasMaxLength(500);

        builder.Property(j => j.ExpirationDate)
            .HasColumnType("date");

        builder.Property(j => j.PublicationType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(EPublicationType.Direct);

        builder.Property(j => j.ApplicationChannel)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(EApplicationChannel.InternalPlatform);

        builder.Property(j => j.CompanyName)
            .HasMaxLength(200);

        builder.Property(j => j.SourceName)
            .HasMaxLength(200);

        builder.Property(j => j.SourceUrl)
            .HasMaxLength(500);

        builder.Property(j => j.ApplicationUrl)
            .HasMaxLength(500);

        builder.Property(j => j.ApplicationContact)
            .HasMaxLength(200);

        builder.Property(j => j.CboCode)
            .HasMaxLength(10);

        builder.HasOne(j => j.Advertiser)
            .WithMany(a => a.Jobs)
            .HasForeignKey(j => j.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.Status, j.CreatedAt })
            .HasDatabaseName("IX_Jobs_Status_CreatedAt");

        builder.HasIndex(j => new { j.AdvertiserId, j.Status })
            .HasDatabaseName("IX_Jobs_AdvertiserId_Status");

        builder.HasIndex(j => j.Title)
            .HasDatabaseName("IX_Jobs_Title");

        builder.HasIndex(j => j.ExpirationDate)
            .HasDatabaseName("IX_Jobs_ExpirationDate");

        builder.OwnsOne(
            j => j.Location,
            location =>
            {
                location.Property(l => l.City)
                    .HasMaxLength(100)
                    .HasColumnName("LocationCity");

                location.Property(l => l.State)
                    .HasMaxLength(5)
                    .HasColumnName("LocationState");

                location.Property(l => l.Country)
                    .HasMaxLength(5)
                    .HasDefaultValue("BR")
                    .IsFixedLength()
                    .HasColumnName("LocationCountry");

                location.Property(l => l.Type)
                    .HasMaxLength(20)
                    .HasDefaultValue("OnSite")
                    .HasColumnName("LocationType");

                location.Property(l => l.IsRemote)
                    .HasDefaultValue(false)
                    .HasColumnName("LocationIsRemote");

                location.Property(l => l.ShowAddress)
                    .HasDefaultValue(true)
                    .HasColumnName("LocationShowAddress");
            });

        builder.OwnsOne(
            j => j.Salary,
            salary =>
            {
                salary.Property(s => s.Min)
                    .HasColumnType("decimal(18,2)")
                    .HasColumnName("SalaryMin");

                salary.Property(s => s.Max)
                    .HasColumnType("decimal(18,2)")
                    .HasColumnName("SalaryMax");

                salary.Property(s => s.Currency)
                    .HasMaxLength(3)
                    .HasDefaultValue("BRL")
                    .HasColumnName("SalaryCurrency");

                salary.Property(s => s.Display)
                    .HasDefaultValue(true)
                    .HasColumnName("SalaryDisplay");

                salary.Property(s => s.Period)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .HasColumnName("SalaryPeriod");
            });

        builder.OwnsOne(
            j => j.Boost,
            boost =>
            {
                boost.Property(b => b.IsActive)
                    .HasDefaultValue(false)
                    .HasColumnName("BoostIsActive");

                boost.Property(b => b.Level)
                    .HasMaxLength(50)
                    .HasColumnName("BoostLevel");

                boost.Property(b => b.StartDate)
                    .HasColumnName("BoostStartDate");

                boost.Property(b => b.EndDate)
                    .HasColumnName("BoostEndDate");
            });

        builder.OwnsOne(
            j => j.Highlight,
            highlight =>
            {
                highlight.Property(h => h.IsActive)
                    .HasDefaultValue(false)
                    .HasColumnName("HighlightIsActive");

                highlight.Property(h => h.StartDate)
                    .HasColumnName("HighlightStartDate");

                highlight.Property(h => h.EndDate)
                    .HasColumnName("HighlightEndDate");
            });

        builder.Navigation(j => j.Highlight).IsRequired();

        builder.OwnsOne(
            j => j.Moderation,
            mod =>
            {
                mod.Property(m => m.Status)
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .HasDefaultValue(EModerationStatus.Pending)
                    .HasColumnName("ModerationStatus");

                mod.Property(m => m.ModeratorId)
                    .HasMaxLength(100)
                    .HasColumnName("ModeratorId");

                mod.Property(m => m.Reason)
                    .HasMaxLength(1000)
                    .HasColumnName("ModerationReason");

                mod.Property(m => m.ModerationDate)
                    .HasColumnName("ModerationDate");
            });

        builder.Navigation(j => j.Moderation).IsRequired();

        builder.OwnsOne(
            j => j.Counters,
            counters =>
            {
                counters.Property(c => c.Views)
                    .HasDefaultValue(0)
                    .HasColumnName("Views");

                counters.Property(c => c.Applications)
                    .HasDefaultValue(0)
                    .HasColumnName("Applications");

                counters.Property(c => c.CompletedJobs)
                    .HasDefaultValue(0)
                    .HasColumnName("CompletedJobs");

                counters.Property(c => c.TotalJobs)
                    .HasDefaultValue(0)
                    .HasColumnName("TotalJobs");
            });
    }
}