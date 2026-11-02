using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using Monburan.Core.Converters;
using Monburan.Core.Entities;
using Monburan.Core.Entities.Base;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data.Extensions;

namespace Monburan.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public ApplicationDbContext() : base(new DbContextOptions<ApplicationDbContext>())
    {
    }

    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<AdminAuditEntry> AdminAuditEntries => Set<AdminAuditEntry>();

    public DbSet<AiProvider> AiProviders => Set<AiProvider>();

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    public DbSet<AccessGroup> AccessGroups => Set<AccessGroup>();

    public DbSet<Advertiser> Advertisers => Set<Advertiser>();

    public DbSet<Application> Applications => Set<Application>();

    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();

    public DbSet<Candidate> Candidates => Set<Candidate>();

    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobAlert> JobAlerts => Set<JobAlert>();

    public DbSet<JobSubmission> JobSubmissions => Set<JobSubmission>();

    public DbSet<JobSubmissionEvidence> JobSubmissionEvidences => Set<JobSubmissionEvidence>();

    public DbSet<JobSubmissionHistory> JobSubmissionHistories => Set<JobSubmissionHistory>();

    public DbSet<ResumeShare> ResumeShares => Set<ResumeShare>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Resume> Resumes => Set<Resume>();

    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();

    public DbSet<TenantConfig> TenantConfigs => Set<TenantConfig>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Toda data é persistida em UTC (ver UtcDateTimeConverter)
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<JobBoost>();
        modelBuilder.Ignore<JobHighlight>();
        modelBuilder.Ignore<JobLocation>();
        modelBuilder.Ignore<SalaryRange>();
        modelBuilder.Ignore<ModerationInfo>();
        modelBuilder.Ignore<JobCounters>();
        modelBuilder.Ignore<Education>();
        modelBuilder.Ignore<Experience>();
        modelBuilder.Ignore<Language>();

        modelBuilder.ApplyEntityConfigurations();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Id) || property.ClrType == typeof(Id))
                {
                    property.SetValueConverter(new IdValueConverter());
                    property.SetValueComparer(new IdValueComparer());
                }
            }
        }
    }

    public override int SaveChanges()
    {
        StampUpdatedAt();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampUpdatedAt();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Carimba a data da última alteração de todo registro alterado. Sem isto, a coluna
    /// <c>UpdatedAt</c> ficava sempre NULL (só o provedor de IA a gravava) e qualquer
    /// ordenação ou auditoria por "atualizado recentemente" lia um valor morto.
    /// </summary>
    private void StampUpdatedAt()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State != EntityState.Modified)
                continue;

            var createdAt = entry.Property(nameof(BaseEntity.CreatedAt)).CurrentValue as DateTime?;
            // A data de alteração precisa ser posterior à de criação
            var updatedAt = now;
            if (createdAt.HasValue && updatedAt <= createdAt.Value)
                updatedAt = createdAt.Value.AddTicks(1);

            entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = updatedAt;
        }
    }
}