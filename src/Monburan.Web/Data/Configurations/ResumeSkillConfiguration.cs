using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class ResumeSkillConfiguration : IEntityTypeConfiguration<ResumeSkill>
{
    public void Configure(EntityTypeBuilder<ResumeSkill> builder)
    {
        builder.ToTable("ResumeSkills");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
    .HasConversion(
        id => id.Value,
        value => Id.Create(value)
    )
    .HasColumnType("uuid");
        builder.HasKey(rs => new { rs.ResumeId, rs.Skill });

        builder.Property(rs => rs.Skill)
            .HasMaxLength(100);

        builder.Property(rs => rs.Level)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(rs => rs.YearsOfExperience)
            .HasDefaultValue(0);

        builder.HasOne(rs => rs.Resume)
            .WithMany(r => r.Skills)
            .HasForeignKey(rs => rs.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rs => rs.Skill)
            .HasDatabaseName("IX_ResumeSkills_Skill");

        builder.HasIndex(rs => new { rs.Skill, rs.Level })
            .HasDatabaseName("IX_ResumeSkills_Skill_Level");
    }
}