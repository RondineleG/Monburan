using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions", table =>
            // Toda assinatura tem exatamente um dono: empresa ou candidato, nunca os dois nem nenhum
            table.HasCheckConstraint("CK_Subscriptions_Owner",
                "(\"AdvertiserId\" IS NOT NULL) <> (\"CandidateId\" IS NOT NULL)"));

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => Id.Create(value)
            )
            .HasColumnType("uuid");

        builder.Property(s => s.AdvertiserId)
            .HasConversion(
                id => id!.Value,
                value => Id.Create(value)
            )
            .HasColumnType("uuid");

        builder.Property(s => s.CandidateId)
            .HasConversion(
                id => id!.Value,
                value => Id.Create(value)
            )
            .HasColumnType("uuid");

        builder.Property(s => s.PlanId)
            .HasConversion(
                id => id.Value,
                value => Id.Create(value)
            )
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.PaymentId)
            .HasMaxLength(100);

        builder.Property(s => s.CancellationReason)
            .HasMaxLength(500);

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.HasOne(s => s.Advertiser)
            .WithMany()
            .HasForeignKey(s => s.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Candidate)
            .WithMany()
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.AdvertiserId)
            .HasDatabaseName("IX_Subscriptions_AdvertiserId");

        builder.HasIndex(s => s.CandidateId)
            .HasDatabaseName("IX_Subscriptions_CandidateId");

        builder.HasIndex(s => s.PlanId)
            .HasDatabaseName("IX_Subscriptions_PlanId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_Subscriptions_Status");

        builder.HasIndex(s => new { s.AdvertiserId, s.Status })
            .HasDatabaseName("IX_Subscriptions_AdvertiserId_Status");
    }
}
