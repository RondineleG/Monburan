using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Monburan.Core.Entities;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data.Configurations;

public class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("Resumes");
        builder.HasKey(a => a.Id);
        builder.Property(e => e.Id)
     .HasConversion(
         id => id.Value,
         value => Id.Create(value)
     )
     .HasColumnType("uuid");
        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Summary)
            .HasMaxLength(2000);

        builder.Property(r => r.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(r => r.Candidate)
            .WithMany(c => c.Resumes)
            .HasForeignKey(r => r.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.Title)
            .HasDatabaseName("IX_Resumes_Title");

        builder.HasIndex(r => new { r.CandidateId, r.IsActive })
            .HasDatabaseName("IX_Resumes_CandidateId_IsActive");

        builder.OwnsMany(
            r => r.EducationHistory,
            education =>
            {
                education.ToTable("EducationHistory");

                education.WithOwner().HasForeignKey("ResumeId");

                education.Property<int>("Id").ValueGeneratedOnAdd();
                education.HasKey("Id");

                education.Property(e => e.Course)
                    .IsRequired()
                    .HasMaxLength(150);

                education.Property(e => e.Institution)
                    .IsRequired()
                    .HasMaxLength(150);

                education.Property(e => e.Level)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                education.Property(e => e.Status)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                education.Property(e => e.StartDate);
                education.Property(e => e.EndDate);
            });

        builder.OwnsMany(
            r => r.ExperienceHistory,
            experience =>
            {
                experience.ToTable("ExperienceHistory");

                experience.WithOwner().HasForeignKey("ResumeId");

                experience.Property<int>("Id").ValueGeneratedOnAdd();
                experience.HasKey("Id");

                experience.Property(e => e.Company)
                    .IsRequired()
                    .HasMaxLength(150);

                experience.Property(e => e.Position)
                    .IsRequired()
                    .HasMaxLength(150);

                experience.Property(e => e.Description)
                    .HasMaxLength(2000);

                experience.Property(e => e.IsCurrent)
                    .HasDefaultValue(false);

                experience.Property(e => e.StartDate);
                experience.Property(e => e.EndDate);
            });

        builder.OwnsMany(
            r => r.Languages,
            lang =>
            {
                lang.ToTable("Languages");

                lang.WithOwner().HasForeignKey("ResumeId");

                lang.Property<int>("Id").ValueGeneratedOnAdd();
                lang.HasKey("Id");

                lang.Property(l => l.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                lang.Property(l => l.Level)
                    .HasConversion<string>()
                    .HasMaxLength(50);
            });
    }
}