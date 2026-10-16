using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        if (context.Advertisers.Any())
            return;

        var tenantId = "default";

        // Seed dos Planos de Assinatura
        var planFree = new SubscriptionPlan
        {
            Id = Id.Create(Guid.Parse("80000000-0000-0000-0000-000000000001")),
            TenantId = tenantId,
            Name = "Free",
            Description = "Plano básico gratuito para pequenas empresas",
            Price = 0,
            MaxJobs = 5,
            HasBoost = false,
            HasHighlight = false,
            HasAnalytics = false,
            MaxResumes = 5,
            IsActive = true
        };

        var planPremium = new SubscriptionPlan
        {
            Id = Id.Create(Guid.Parse("80000000-0000-0000-0000-000000000002")),
            TenantId = tenantId,
            Name = "Premium",
            Description = "Plano intermediário com recursos essenciais",
            Price = 12.90m,
            MaxJobs = 10,
            HasBoost = true,
            HasHighlight = true,
            HasAnalytics = false,
            MaxResumes = 25,
            IsActive = true
        };

        var planEnterprise = new SubscriptionPlan
        {
            Id = Id.Create(Guid.Parse("80000000-0000-0000-0000-000000000003")),
            TenantId = tenantId,
            Name = "Enterprise",
            Description = "Plano completo para grandes empresas",
            Price = 49.90m,
            MaxJobs = -1, // Ilimitado
            HasBoost = true,
            HasHighlight = true,
            HasAnalytics = true,
            MaxResumes = -1, // Ilimitado
            IsActive = true
        };

        context.SubscriptionPlans.AddRange(planFree, planPremium, planEnterprise);
        await context.SaveChangesAsync();

        var tenantConfig = new TenantConfig
        {
            Id = Id.Create(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            TenantId = tenantId,
            DisplayName = "Monburan",
            BaseUrl = "https://monburan.com.br",
            IsActive = true,
            PrimaryThemeColor = "#0d6efd"
        };
        context.TenantConfigs.Add(tenantConfig);

        var addressSP = new Address
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = tenantId,
            City = "São Paulo",
            State = "SP",
            Country = "BR",
            District = "Centro",
            Street = "Av. Paulista",
            Number = "1000",
            ZipCode = "01310-100"
        };

        var addressRJ = new Address
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = tenantId,
            City = "Rio de Janeiro",
            State = "RJ",
            Country = "BR",
            District = "Centro",
            Street = "Av. Rio Branco",
            Number = "500",
            ZipCode = "20040-001"
        };

        var addressRemote = new Address
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = tenantId,
            City = "Remoto",
            State = "SP",
            Country = "BR"
        };

        context.Addresses.AddRange(addressSP, addressRJ, addressRemote);
        await context.SaveChangesAsync();

        var advertiser1 = new Advertiser
        {
            Id = Id.Create(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            TenantId = tenantId,
            Name = "Tech Solutions Ltda",
            Email = "contato@techsolutions.com",
            Phone = "(11) 99999-9999",
            TaxId = "12.345.678/0001-90",
            PersonType = EPersonType.Company,
            AddressId = addressSP.Id,
            Status = EAdvertiserStatus.Active,
            IsEmailVerified = true,
            CompanyDescription = "Empresa especializada em soluções de tecnologia",
            LogoUrl = null,
            Website = "https://techsolutions.com",
            ActivePlan = "Free"
        };

        var advertiser2 = new Advertiser
        {
            Id = Id.Create(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            TenantId = tenantId,
            Name = "Marketing Digital SA",
            Email = "rh@marketingdigital.com",
            Phone = "(21) 98888-8888",
            TaxId = "98.765.432/0001-10",
            PersonType = EPersonType.Company,
            AddressId = addressRJ.Id,
            Status = EAdvertiserStatus.Active,
            IsEmailVerified = true,
            CompanyDescription = "Agência de marketing digital",
            LogoUrl = null,
            Website = "https://marketingdigital.com",
            ActivePlan = "Free"
        };

        context.Advertisers.AddRange(advertiser1, advertiser2);
        await context.SaveChangesAsync();

        // Seed de assinatura de teste para advertiser1
        var testSubscription = new Subscription
        {
            Id = Id.Create(Guid.Parse("90000000-0000-0000-0000-000000000001")),
            TenantId = tenantId,
            AdvertiserId = advertiser1.Id,
            PlanId = planPremium.Id,
            StartDate = DateTime.UtcNow.AddDays(-15),
            EndDate = DateTime.UtcNow.AddDays(15),
            Status = ESubscriptionStatus.Active,
            PaymentId = "test_payment_123"
        };

        context.Subscriptions.Add(testSubscription);
        await context.SaveChangesAsync();

        var candidate1 = new Candidate
        {
            Id = Id.Create(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            TenantId = tenantId,
            Name = "João Silva",
            Email = "joao.silva@email.com",
            Phone = "(11) 97777-7777",
            DateOfBirth = new DateTime(1990, 5, 15),
            AddressId = addressSP.Id,
            Status = EAccountStatus.Active,
            IsEmailVerified = true,
            LinkedIn = "https://linkedin.com/in/joaosilva"
        };

        var candidate2 = new Candidate
        {
            Id = Id.Create(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            TenantId = tenantId,
            Name = "Maria Santos",
            Email = "maria.santos@email.com",
            Phone = "(21) 96666-6666",
            DateOfBirth = new DateTime(1992, 8, 20),
            AddressId = addressRJ.Id,
            Status = EAccountStatus.Active,
            IsEmailVerified = true,
            LinkedIn = "https://linkedin.com/in/mariasantos"
        };

        context.Candidates.AddRange(candidate1, candidate2);
        await context.SaveChangesAsync();

        var jobs = new List<Job>
        {
            new()
            {
                Id = Id.Create(Guid.Parse("60000000-0000-0000-0000-000000000001")),
                TenantId = tenantId,
                AdvertiserId = advertiser1.Id,
                Title = "Desenvolvedor Full Stack Pleno",
                Description = "Buscamos desenvolvedor full stack experiente.",
                Requirements = ".NET, React, SQL Server",
                Category = "Tecnologia",
                Tags = ".NET, React, C#",
                Status = EJobStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(60),
                Location = new JobLocation { City = "São Paulo", State = "SP", Type = "Hybrid", IsRemote = true },
                Salary = new SalaryRange { Min = 8000, Max = 12000, Display = true },
                Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = false },
                Counters = new JobCounters { Views = 45, Applications = 12 }
            },
            new()
            {
                Id = Id.Create(Guid.Parse("60000000-0000-0000-0000-000000000002")),
                TenantId = tenantId,
                AdvertiserId = advertiser2.Id,
                Title = "Analista de Marketing Digital",
                Description = "Procuramos analista de marketing digital criativo.",
                Requirements = "Google Ads, SEO, Analytics",
                Category = "Marketing",
                Tags = "Marketing, SEO",
                Status = EJobStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Location = new JobLocation { City = "Rio de Janeiro", State = "RJ", Type = "Remote", IsRemote = true },
                Salary = new SalaryRange { Min = 5000, Max = 7000, Display = true },
                Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = false },
                Counters = new JobCounters { Views = 92, Applications = 31 }
            },
            new()
            {
                Id = Id.Create(Guid.Parse("60000000-0000-0000-0000-000000000003")),
                TenantId = tenantId,
                AdvertiserId = advertiser1.Id,
                Title = "Desenvolvedor Backend Senior",
                Description = "Vaga para desenvolvedor backend com experiência em arquitetura de microsserviços.",
                Requirements = "C#, .NET Core, Docker, Kubernetes",
                Category = "Tecnologia",
                Tags = "Backend, C#, Docker",
                Status = EJobStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(45),
                Location = new JobLocation { City = "São Paulo", State = "SP", Type = "OnSite", IsRemote = false },
                Salary = new SalaryRange { Min = 15000, Max = 20000, Display = true },
                Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = false },
                Counters = new JobCounters { Views = 78, Applications = 23 }
            },
            new()
            {
                Id = Id.Create(Guid.Parse("60000000-0000-0000-0000-000000000004")),
                TenantId = tenantId,
                AdvertiserId = advertiser2.Id,
                Title = "Gerente de Projetos",
                Description = "Gerenciamento de projetos de marketing digital para clientes de grande porte.",
                Requirements = "PMP, Scrum, experiência em gestão de equipes",
                Category = "Gestão",
                Tags = "Projetos, Gestão, Scrum",
                Status = EJobStatus.Active,
                ExpirationDate = DateTime.UtcNow.AddDays(90),
                Location = new JobLocation { City = "Rio de Janeiro", State = "RJ", Type = "Hybrid", IsRemote = true },
                Salary = new SalaryRange { Min = 12000, Max = 16000, Display = true },
                Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = false },
                Counters = new JobCounters { Views = 56, Applications = 18 }
            },
            new()
            {
                Id = Id.Create(Guid.Parse("60000000-0000-0000-0000-000000000005")),
                TenantId = tenantId,
                AdvertiserId = advertiser1.Id,
                Title = "Desenvolvedor Frontend Junior",
                Description = "Oportunidade para desenvolvedor frontend iniciante.",
                Requirements = "HTML, CSS, JavaScript, React básico",
                Category = "Tecnologia",
                Tags = "Frontend, React, JavaScript",
                Status = EJobStatus.Paused,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Location = new JobLocation { City = "Remoto", State = "SP", Type = "Remote", IsRemote = true },
                Salary = new SalaryRange { Min = 3000, Max = 5000, Display = true },
                Moderation = new ModerationInfo { Status = EModerationStatus.Pending },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = false },
                Counters = new JobCounters { Views = 15, Applications = 5 }
            }
        };

        context.Jobs.AddRange(jobs);
        await context.SaveChangesAsync();

        // Criar candidaturas de exemplo
        var applications = new List<Application>
        {
            new()
            {
                Id = Id.Create(Guid.Parse("70000000-0000-0000-0000-000000000001")),
                TenantId = tenantId,
                CandidateId = candidate1.Id,
                JobId = jobs[0].Id,
                Status = EApplicationStatus.Sent,
                AppliedAt = DateTime.UtcNow.AddDays(-5),
                Notes = "Tenho grande interesse na vaga e acredito que meu perfil se encaixa perfeitamente."
            },
            new()
            {
                Id = Id.Create(Guid.Parse("70000000-0000-0000-0000-000000000002")),
                TenantId = tenantId,
                CandidateId = candidate2.Id,
                JobId = jobs[0].Id,
                Status = EApplicationStatus.Viewed,
                AppliedAt = DateTime.UtcNow.AddDays(-4),
                ViewedAt = DateTime.UtcNow.AddDays(-3),
                Notes = "Experiência sólida em .NET e React."
            },
            new()
            {
                Id = Id.Create(Guid.Parse("70000000-0000-0000-0000-000000000003")),
                TenantId = tenantId,
                CandidateId = candidate1.Id,
                JobId = jobs[1].Id,
                Status = EApplicationStatus.Selected,
                AppliedAt = DateTime.UtcNow.AddDays(-10),
                ViewedAt = DateTime.UtcNow.AddDays(-8),
                SelectedAt = DateTime.UtcNow.AddDays(-2),
                Notes = "Transição de carreira para marketing digital."
            },
            new()
            {
                Id = Id.Create(Guid.Parse("70000000-0000-0000-0000-000000000004")),
                TenantId = tenantId,
                CandidateId = candidate2.Id,
                JobId = jobs[2].Id,
                Status = EApplicationStatus.Rejected,
                AppliedAt = DateTime.UtcNow.AddDays(-15),
                ViewedAt = DateTime.UtcNow.AddDays(-12),
                RejectedAt = DateTime.UtcNow.AddDays(-7),
                Notes = "Não possui experiência com Kubernetes."
            },
            new()
            {
                Id = Id.Create(Guid.Parse("70000000-0000-0000-0000-000000000005")),
                TenantId = tenantId,
                CandidateId = candidate1.Id,
                JobId = jobs[3].Id,
                Status = EApplicationStatus.Sent,
                AppliedAt = DateTime.UtcNow.AddDays(-1),
                Notes = "Experiência em gestão de projetos ágeis."
            }
        };

        context.Applications.AddRange(applications);
        await context.SaveChangesAsync();
    }
}
