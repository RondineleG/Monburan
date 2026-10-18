using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class AiProviderConfiguration : IEntityTypeConfiguration<AiProvider>
{
    public void Configure(EntityTypeBuilder<AiProvider> builder)
    {
        builder.ToTable("AiProviders");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => Id.Create(value))
            .HasColumnType("uuid");

        builder.Property(p => p.Name).IsRequired().HasMaxLength(80);
        builder.Property(p => p.Protocol).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.BaseUrl).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Model).IsRequired().HasMaxLength(150);
        // Só o NOME da variável de ambiente (AI_KEY_...): a chave nunca vai para o banco
        builder.Property(p => p.ApiKeyEnvVar).IsRequired().HasMaxLength(70);

        // Nome único entre os não excluídos, e no máximo um padrão
        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("IX_AiProviders_Name");

        builder.HasIndex(p => p.IsDefault)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false")
            .HasDatabaseName("IX_AiProviders_IsDefault");
    }
}
