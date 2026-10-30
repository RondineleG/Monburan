using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("FeatureFlags");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(60);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Groups).IsRequired().HasMaxLength(500);
        builder.Ignore(e => e.GroupList);

        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("IX_FeatureFlags_Name");
    }
}

public class AccessGroupConfiguration : IEntityTypeConfiguration<AccessGroup>
{
    // Grupos iniciais: as duas turmas de teste com usuários de verdade
    private static readonly DateTime SeededAt = new(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AccessGroup> builder)
    {
        builder.ToTable("AccessGroups");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(AccessGroups.MaxNameLength);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(300);

        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("IX_AccessGroups_Name");

        builder.HasData(
            Seed("a0000000-0000-0000-0000-000000000001", "candidato-beta", "Candidatos que testam funcionalidades novas antes de todos"),
            Seed("a0000000-0000-0000-0000-000000000002", "recrutador-beta", "Empresas que testam funcionalidades novas antes de todos"));
    }

    private static object Seed(string id, string name, string description) => new
    {
        Id = Id.Create(Guid.Parse(id)),
        Name = name,
        Description = description,
        CreatedAt = SeededAt,
        CreatedBy = string.Empty,
        DeletedBy = string.Empty,
        UpdatedBy = string.Empty,
        IsDeleted = false,
        TenantId = "default"
    };
}
