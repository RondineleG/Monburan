using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.UseCases.Applications;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.UseCases.Advertisers;
using Monburan.Core.UseCases.Resumes;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.UseCases.Storage;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.Validations;
using Monburan.Web.Middleware;
using Monburan.Web.Repositories;
using Monburan.Web.Services;
using Serilog;

try
{
    Log.Information("Iniciando aplicação Monburan");

    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Monburan")
        .CreateLogger();

    // Usar Serilog
    builder.Host.UseSerilog();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Adicionar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Monburan API",
        Version = "v1",
        Description = "API para plataforma de recrutamento Monburan"
    });
});

// Adicionar Health Checks
builder.Services.AddHealthChecks();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Registrar EmailSender (SendGrid com fallback para NoOp em desenvolvimento)
builder.Services.AddScoped<Monburan.Core.Interfaces.Services.IEmailSender, Monburan.Web.Services.SendGridEmailSender>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser>, IdentityEmailSender>();

// Registrar Repositórios
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IBaseRepository<Job>, JobRepository>(); // Para UseCases que usam IBaseRepository
builder.Services.AddScoped<IBaseRepository<Candidate>, CandidateRepository>();
builder.Services.AddScoped<IBaseRepository<Advertiser>, AdvertiserRepository>();
builder.Services.AddScoped<IBaseRepository<Application>, ApplicationRepository>();
builder.Services.AddScoped<IBaseRepository<Resume>, ResumeRepository>();
builder.Services.AddScoped<IBaseRepository<Notification>, NotificationRepository>();
builder.Services.AddScoped<IBaseRepository<TenantConfig>, TenantConfigRepository>();

// Registrar Repositórios de Assinatura
builder.Services.AddScoped<IBaseRepository<SubscriptionPlan>, SubscriptionPlanRepository>();
builder.Services.AddScoped<IBaseRepository<Subscription>, SubscriptionRepository>();
builder.Services.AddScoped<SubscriptionRepository>();
builder.Services.AddScoped<SubscriptionPlanRepository>();

// Registrar Validadores
builder.Services.AddScoped<IEntityValidator<Job>, JobValidator>();
builder.Services.AddScoped<IEntityValidator<Candidate>, CandidateValidator>();
builder.Services.AddScoped<IEntityValidator<Advertiser>, AdvertiserValidator>();
builder.Services.AddScoped<IEntityValidator<Resume>, ResumeValidator>();

// Registrar Use Cases de Jobs
builder.Services.AddScoped<CreateJobUseCase>();
builder.Services.AddScoped<ListJobsUseCase>();
builder.Services.AddScoped<GetJobByIdUseCase>();
builder.Services.AddScoped<UpdateJobUseCase>();
builder.Services.AddScoped<DeleteJobUseCase>();
builder.Services.AddScoped<SearchJobsUseCase>();

// Registrar Use Cases de Applications
builder.Services.AddScoped<CreateApplicationUseCase>();
builder.Services.AddScoped<ListApplicationsByJobUseCase>();
builder.Services.AddScoped<ListApplicationsByCandidateUseCase>();
builder.Services.AddScoped<UpdateApplicationStatusUseCase>();

// Registrar Use Cases de Notifications
builder.Services.AddScoped<NotifyOnApplicationCreatedUseCase>();
builder.Services.AddScoped<NotifyOnApplicationStatusChangedUseCase>();
builder.Services.AddScoped<NotifyOnJobExpiredUseCase>();

// Registrar Use Cases de Candidates
builder.Services.AddScoped<CreateCandidateUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Candidates.ListCandidatesUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Candidates.GetCandidateByIdUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Candidates.DeleteCandidateUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Candidates.UpdateCandidateUseCase>();

// Registrar Use Cases de Advertisers
builder.Services.AddScoped<CreateAdvertiserUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Advertisers.ListAdvertisersUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Advertisers.GetAdvertiserByIdUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Advertisers.DeleteAdvertiserUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Advertisers.UpdateAdvertiserUseCase>();

// Registrar Use Cases de Resumes
builder.Services.AddScoped<CreateResumeUseCase>();
builder.Services.AddScoped<UpdateResumeUseCase>();
builder.Services.AddScoped<DeleteResumeUseCase>();
builder.Services.AddScoped<ListResumesByCandidateUseCase>();
builder.Services.AddScoped<GetResumeByIdUseCase>();

// Registrar Validadores
builder.Services.AddScoped<IEntityValidator<Resume>, ResumeValidator>();

// Registrar Use Cases de Notifications
builder.Services.AddScoped<CreateNotificationUseCase>();
builder.Services.AddScoped<ListNotificationsByUserUseCase>();
builder.Services.AddScoped<MarkNotificationAsReadUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Notifications.SendEmailNotificationUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Notifications.EmailConfirmationUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Notifications.PasswordResetUseCase>();

// Registrar Use Cases de Assinatura
builder.Services.AddScoped<GetAvailablePlansUseCase>();
builder.Services.AddScoped<UpgradePlanUseCase>();
builder.Services.AddScoped<CancelSubscriptionUseCase>();
builder.Services.AddScoped<CheckJobLimitUseCase>();

// Registrar Storage Provider
builder.Services.AddScoped<IStorageProvider, LocalStorageProvider>();

// Registrar Use Case de Upload
builder.Services.AddScoped<UploadFileUseCase>();

// Registrar CurrentUserHelper
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserHelper>();
builder.Services.AddScoped<ICurrentUserHelper>(services =>
    services.GetRequiredService<CurrentUserHelper>());

// Registrar UseCase de Analytics
builder.Services.AddScoped<Monburan.Web.UseCases.Analytics.GetDashboardAnalyticsUseCase>();

// Registrar Background Services
builder.Services.AddHostedService<JobExpirationBackgroundService>();

// Serviços existentes
builder.Services.AddScoped<AddressRepository>();
builder.Services.AddScoped<AddressService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Monburan API v1");
    });
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures("pt-BR")
    .AddSupportedUICultures("pt-BR"));
app.UseRateLimit(100); // 100 requests per second
app.UseSecurityHeaders();
app.UseStatusCodePagesWithReExecute("/404");
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Monburan.Web.Client._Imports).Assembly);

app.MapAdditionalIdentityEndpoints();

// Health Check endpoint
app.MapHealthChecks("/health");

// Aplicar migrations automaticamente quando APPLY_MIGRATIONS=true (deploy simples em VPS)
if (app.Configuration.GetValue<bool>("APPLY_MIGRATIONS"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Migrations aplicadas com sucesso");
}

// Seed Data em desenvolvimento
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await SeedData.InitializeAsync(context);
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await DevelopmentIdentitySeedData.InitializeAsync(userManager, roleManager);
}

Log.Information("Aplicação Monburan iniciada com sucesso");
app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação Monburan falhou ao iniciar");
}
finally
{
    Log.CloseAndFlush();
}