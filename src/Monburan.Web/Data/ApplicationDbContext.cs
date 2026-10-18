using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using Monburan.Core.Converters;
using Monburan.Core.Entities;
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

    public DbSet<AiProvider> AiProviders => Set<AiProvider>();

    public DbSet<Advertiser> Advertisers => Set<Advertiser>();

    public DbSet<Application> Applications => Set<Application>();

    public DbSet<Candidate> Candidates => Set<Candidate>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Resume> Resumes => Set<Resume>();

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
}