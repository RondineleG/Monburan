using Microsoft.AspNetCore.Authentication.JwtBearer;

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
using Monburan.Web.Configurations;
using Monburan.Web.Endpoints;
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
// Enums como string nas minimal APIs (ex.: UpdateApplicationStatusRequest.NewStatus
// aceita "Selected"; NotificationResponse.Type emite "NewApplication")
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

// JWT para a API mobile (emissor: IdentityService; validador: AddJwtBearer)
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "Jwt:SigningKey é obrigatório (defina via appsettings ou variável de ambiente Jwt__SigningKey).")
    .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey precisa de pelo menos 32 bytes (HMAC-SHA256).")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Issuer e Jwt:Audience são obrigatórios.")
    .ValidateOnStart();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<AccountProfileService>();

// Feedback de UI: escopo por circuito (página e ToastHost do layout compartilham a instância)
builder.Services.AddScoped<IToastService, ToastService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    options.TokenValidationParameters = jwt.ToTokenValidationParameters();
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            // API responde 401 em JSON, nunca redirect de cookie
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        }
    };
})
.AddIdentityCookies();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    // Páginas por papel: o perfil vem da claim user_type emitida no login
    .AddPolicy("CandidateOnly", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimsExtensions.UserTypeClaimType, "candidate"))
    .AddPolicy("AdvertiserOnly", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimsExtensions.UserTypeClaimType, "advertiser"))
    // token_use=access impede que um refresh token (JWT válido por 30 dias)
    // autentique como bearer em endpoints que só exigem RequireAuthenticatedUser
    .AddPolicy("ApiBearer", policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(IdentityService.TokenUseClaim, IdentityService.TokenUseAccess));

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
.AddErrorDescriber<PortugueseIdentityErrorDescriber>()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Registrar EmailSender: SMTP (Resend) quando Smtp:Host está configurado, senão SendGrid.
builder.Services.AddScoped<Monburan.Web.Services.SmtpEmailSender>();
builder.Services.AddScoped<Monburan.Web.Services.SendGridEmailSender>();
builder.Services.AddScoped<Monburan.Core.Interfaces.Services.IEmailSender>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return !string.IsNullOrWhiteSpace(configuration["Smtp:Host"])
        ? sp.GetRequiredService<Monburan.Web.Services.SmtpEmailSender>()
        : sp.GetRequiredService<Monburan.Web.Services.SendGridEmailSender>();
});
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser>, IdentityEmailSender>();

// Registrar Repositórios
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IBaseRepository<Job>, JobRepository>(); // Para UseCases que usam IBaseRepository
builder.Services.AddScoped<IBaseRepository<Candidate>, CandidateRepository>();
builder.Services.AddScoped<ICandidateRepository, CandidateRepository>();
builder.Services.AddScoped<IBaseRepository<Advertiser>, AdvertiserRepository>();
builder.Services.AddScoped<IAdvertiserRepository, AdvertiserRepository>();
builder.Services.AddScoped<IBaseRepository<Application>, ApplicationRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IBaseRepository<Resume>, ResumeRepository>();
builder.Services.AddScoped<IResumeRepository, ResumeRepository>();
builder.Services.AddScoped<IBaseRepository<Notification>, NotificationRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IBaseRepository<TenantConfig>, TenantConfigRepository>();

// Registrar Repositórios de Assinatura
builder.Services.AddScoped<IBaseRepository<SubscriptionPlan>, SubscriptionPlanRepository>();
builder.Services.AddScoped<IBaseRepository<Subscription>, SubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
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
builder.Services.AddScoped<ChangeJobStatusUseCase>();

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
builder.Services.AddScoped<UpdateAdvertiserLogoUseCase>();

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
builder.Services.AddScoped<MarkAllNotificationsAsReadUseCase>();
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
// Re-execute para /404 só fora da API — rotas /api/* inexistentes respondem 404 vazio, não HTML do site
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/404"));
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Monburan.Web.Client._Imports).Assembly);

app.MapAdditionalIdentityEndpoints();

// API REST consumida pelo app mobile
app.MapApiEndpoints();

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
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException é o encerramento normal usado pelas ferramentas do EF (migrations)
    Log.Fatal(ex, "Aplicação Monburan falhou ao iniciar");
}
finally
{
    Log.CloseAndFlush();
}

// Necessário para WebApplicationFactory<Program> nos testes de API
public partial class Program;