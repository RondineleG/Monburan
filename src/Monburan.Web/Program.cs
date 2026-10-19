using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using System.Security.Claims;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.UseCases.Ai;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.UseCases.JobAlerts;
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

// ProblemDetails (RFC 7807) para toda resposta de erro da API, com títulos pt-BR
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblemDetails.Localize);

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
builder.Services.AddSingleton<IAuthorizationHandler, AdminTwoFactorHandler>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ActiveAccountChecker>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<AccountProfileService>();
builder.Services.AddScoped<AdminAccountService>();

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
        // O token vale até expirar; uma conta inativada pela administração deixa de usar a API em até 1 minuto
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                ?? context.Principal?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var checker = context.HttpContext.RequestServices.GetRequiredService<ActiveAccountChecker>();
            if (userId is null || !await checker.IsActiveAsync(userId))
                context.Fail("Conta inativa ou inexistente.");
        },
        OnChallenge = context =>
        {
            // API responde 401 em ProblemDetails, nunca redirect de cookie
            context.HandleResponse();
            return ApiProblemDetails.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                "Sua sessão expirou ou é inválida. Entre novamente.");
        },
        OnForbidden = context => ApiProblemDetails.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
            "Você não tem permissão para esta ação.")
    };
})
.AddIdentityCookies();

builder.Services.AddAuthorizationBuilder()
    // Administrador: papel Admin e, fora do desenvolvimento, verificação em duas etapas ligada na conta
    .AddPolicy("AdminOnly", policy => policy
        .RequireRole("Admin")
        .AddRequirements(new AdminTwoFactorRequirement()))
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
    options.Password.RequiredLength = PasswordPolicy.MinLength;

    // Bloqueio por tentativas: depois de 5 senhas erradas seguidas a conta fica 15 minutos sem aceitar login (a API e a web
    // contam as falhas; o acerto zera a contagem)
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager<ApplicationSignInManager>()
.AddDefaultTokenProviders()
.AddErrorDescriber<PortugueseIdentityErrorDescriber>()
.AddPasswordValidator<CommonPasswordValidator>()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Limites de requisição por IP (geral e, mais rígido, nos envios de login, cadastro e recuperação de senha)
builder.Services.AddOptions<SecurityOptions>().Bind(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>().Configure<IOptionsMonitor<SecurityOptions>>(RateLimiting.Configure);

// Atrás do nginx: IP real e https vêm dos cabeçalhos X-Forwarded-* (só de proxy local ou da rede interna)
builder.Services.Configure<ForwardedHeadersOptions>(ForwardedHeadersSetup.Apply);

// Cookies de sessão e antiforgery: Secure sempre fora do desenvolvimento, HttpOnly, SameSite=Lax
builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, CookieSecuritySetup>();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecuritySetup.SecurePolicyFor(builder.Environment);
});

// Chaves do DataProtection (cookie de login, antiforgery e tokens dos links de e-mail). Em produção ficam numa pasta
// persistente (volume do contêiner); sem ela, cada deploy recria o contêiner, gera chaves novas e desloga todo mundo.
// A pasta é lida só quando o serviço é criado, com a configuração final (variável de ambiente ou appsettings).
builder.Services.AddDataProtection().SetApplicationName("Monburan");
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
{
    var keysPath = services.GetRequiredService<IConfiguration>()["DataProtection:KeysPath"];
    var loggerFactory = services.GetService<ILoggerFactory>() ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
    return new ConfigureOptions<KeyManagementOptions>(options =>
    {
        if (!string.IsNullOrWhiteSpace(keysPath))
            options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(keysPath), loggerFactory);
    });
});

// Administrador sem 2FA que tenta abrir uma área de administração não vê "acesso negado": vai para a tela de configuração
builder.Services.ConfigureApplicationCookie(options =>
{
    var accessDenied = options.Events.OnRedirectToAccessDenied;
    options.Events.OnRedirectToAccessDenied = context =>
    {
        var security = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<SecurityOptions>>().CurrentValue;
        var user = context.HttpContext.User;
        if (security.RequireTwoFactorForAdmins && user.IsInRole("Admin") && !AdminTwoFactorHandler.HasTwoFactor(user))
        {
            context.Response.Redirect("/Account/Manage/TwoFactorAuthentication?adminRequired=true");
            return Task.CompletedTask;
        }

        return accessDenied(context);
    };
});

// A sessão é revalidada no banco a cada 2 minutos (padrão: 30): conta desativada, senha trocada ou 2FA ligado valem logo
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(2));

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

// IA (sugestões de melhoria de currículo): os provedores são cadastrados pelo administrador na tela "Provedores de IA".
// O banco guarda endereço, modelo, protocolo e o NOME da variável de ambiente da chave; a chave vem do ambiente do
// servidor e nunca é guardada. Sem provedor padrão ativo e com chave, o recurso fica desligado (não aparece no site nem no app).
builder.Services.AddOptions<AiOptions>().Bind(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAiUsageLimiter, MemoryAiUsageLimiter>();
builder.Services.AddSingleton<IAiSecretReader, AiSecretReader>();
builder.Services.AddSingleton<IAiProviderCatalog, AiProviderCatalog>();
builder.Services.AddHostedService<AiProviderCatalogRefresher>();

// Cliente HTTP dos provedores: o tempo limite é de cada provedor (não do HttpClient), sem redirecionamentos, resposta
// limitada e uma conexão que recusa rede interna e reservada (o endereço do provedor é editável pelo administrador)
builder.Services.AddHttpClient(AiHttp.ClientName, client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AiOptions>>();
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            // Sem proxy: com ele a conexão iria para o proxy e a guarda de rede não veria o destino de verdade
            UseProxy = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(15),
            ConnectCallback = (context, cancellationToken) =>
                AiNetworkGuard.ConnectAsync(context, options.CurrentValue.AllowPrivateNetworks, cancellationToken)
        };
    });
builder.Services.AddSingleton<IAiProtocol>(serviceProvider => new OpenAiProtocol(
    serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(AiHttp.ClientName),
    serviceProvider.GetRequiredService<ILogger<OpenAiProtocol>>()));
builder.Services.AddSingleton<IAiProtocol>(serviceProvider => new AnthropicProtocol(
    serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(AiHttp.ClientName),
    serviceProvider.GetRequiredService<ILogger<AnthropicProtocol>>()));
builder.Services.AddSingleton<IAiTextClient, AiTextClient>();

// Registrar Repositórios
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IBaseRepository<Job>, JobRepository>(); // Para UseCases que usam IBaseRepository
builder.Services.AddScoped<IBaseRepository<Candidate>, CandidateRepository>();
builder.Services.AddScoped<ICandidateRepository, CandidateRepository>();
builder.Services.AddScoped<IBaseRepository<Advertiser>, AdvertiserRepository>();
builder.Services.AddScoped<IAdvertiserRepository, AdvertiserRepository>();
builder.Services.AddScoped<IBaseRepository<Application>, ApplicationRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IApplicationStatusHistoryRepository, ApplicationStatusHistoryRepository>();
builder.Services.AddScoped<IBaseRepository<Resume>, ResumeRepository>();
builder.Services.AddScoped<IResumeRepository, ResumeRepository>();
builder.Services.AddScoped<ISavedJobRepository, SavedJobRepository>();
builder.Services.AddScoped<IJobAlertRepository, JobAlertRepository>();
builder.Services.AddScoped<ICareerProfileRepository, CareerProfileRepository>();
builder.Services.AddScoped<IAiProviderRepository, AiProviderRepository>();
builder.Services.AddScoped<IAdminAuditLog, AdminAuditLog>();
builder.Services.AddScoped<Monburan.Core.UseCases.Admin.AdminListJobsUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Admin.AdminChangeJobStatusUseCase>();
builder.Services.AddScoped<Monburan.Core.UseCases.Admin.AdminDeleteJobUseCase>();
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
builder.Services.AddScoped<SaveJobUseCase>();
builder.Services.AddScoped<UnsaveJobUseCase>();
builder.Services.AddScoped<ListSavedJobsUseCase>();
builder.Services.AddScoped<ListRecommendedJobsUseCase>();
builder.Services.AddScoped<RegisterJobViewUseCase>();

// Registrar Use Cases de Alertas de Vaga
builder.Services.AddScoped<ListJobAlertsUseCase>();
builder.Services.AddScoped<CreateJobAlertUseCase>();
builder.Services.AddScoped<UpdateJobAlertUseCase>();
builder.Services.AddScoped<DeleteJobAlertUseCase>();
builder.Services.AddScoped<NotifyJobAlertsOnJobCreatedUseCase>();

// Registrar Use Cases de Applications
builder.Services.AddScoped<CreateApplicationUseCase>();
builder.Services.AddScoped<ListApplicationsByJobUseCase>();
builder.Services.AddScoped<ListApplicationsByCandidateUseCase>();
builder.Services.AddScoped<UpdateApplicationStatusUseCase>();
builder.Services.AddScoped<ListApplicationHistoryUseCase>();

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
builder.Services.AddScoped<GetCareerProfileUseCase>();
builder.Services.AddScoped<UpsertCareerProfileUseCase>();
builder.Services.AddScoped<GetCandidateProgressUseCase>();

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
builder.Services.AddScoped<ImproveResumeUseCase>();

// Provedores de IA (administração)
builder.Services.AddScoped<ListAiProvidersUseCase>();
builder.Services.AddScoped<SaveAiProviderUseCase>();
builder.Services.AddScoped<SetDefaultAiProviderUseCase>();
builder.Services.AddScoped<DeleteAiProviderUseCase>();
builder.Services.AddScoped<TestAiProviderUseCase>();
builder.Services.AddScoped<SeedAiProvidersUseCase>();

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
builder.Services.AddScoped<CircuitUserAccessor>();
builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, UserCircuitHandler>());
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

// Antes de tudo (HSTS, redirecionamento para https, autenticação, limites): esquema e IP reais do cliente
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(app.Configuration["DataProtection:KeysPath"]))
    Log.Warning("DataProtection:KeysPath não definido: as chaves de sessão não sobrevivem ao contêiner e cada deploy desloga todos");

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
    // API: exceção não tratada vira ProblemDetails (JSON) — antes o app recebia a página
    // HTML de erro e não conseguia ler a resposta. Site continua na página /Error.
    app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"),
        api => api.UseExceptionHandler());
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
        web => web.UseExceptionHandler("/Error", createScopeForErrors: true));
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures("pt-BR")
    .AddSupportedUICultures("pt-BR"));
app.UseRateLimiter();
app.UseSecurityHeaders();
// Rota inexistente no site chega ao navegador com a página /404 renderizada. A re-execução precisa ficar no pipeline
// externo, fora de um branch do UseWhen: o pipeline de um branch termina nele mesmo e não alcança o middleware de
// endpoints global, então o /404 re-executado nunca casava com o endpoint e o corpo saía vazio.
// A API não é afetada: o UseStatusCodePages abaixo escreve o ProblemDetails antes desta linha olhar a resposta.
app.UseStatusCodePagesWithReExecute("/404");
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePages());
// Caminho com extensão é arquivo (imagem, CSS, fonte), não página: quando a re-execução veio de um arquivo, responde
// 404 seco em vez de renderizar o site inteiro para quem pediu um asset que não existe.
app.Use(async (context, next) =>
{
    var reExecute = context.Features.Get<IStatusCodeReExecuteFeature>();
    if (reExecute is not null && HasFileExtension(new PathString(reExecute.OriginalPath)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    // O Blazor acrescentaria um segundo Content-Security-Policy (frame-ancestors 'self'); a política única, com
    // frame-ancestors 'none', vem do SecurityHeadersMiddleware
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null)
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Monburan.Web.Client._Imports).Assembly);

app.MapAdditionalIdentityEndpoints();

// API REST consumida pelo app mobile
app.MapApiEndpoints();
app.MapSeoEndpoints();

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

// Provedores de IA: cadastra os modelos iniciais na primeira execução e carrega o catálogo em memória. Uma falha aqui
// não pode impedir o site de subir: a IA só fica desligada até a próxima releitura.
try
{
    using var scope = app.Services.CreateScope();
    var seeded = await scope.ServiceProvider.GetRequiredService<SeedAiProvidersUseCase>().ExecuteAsync();
    if (seeded > 0)
        Log.Information("IA: {Count} provedores iniciais cadastrados", seeded);

    await app.Services.GetRequiredService<IAiProviderCatalog>().ReloadAsync();
}
catch (Exception ex)
{
    Log.Warning(ex, "IA: não foi possível preparar os provedores no início; o recurso fica desligado até a próxima releitura");
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

// Caminho com extensão é arquivo (imagem, CSS, fonte), não página: um 404 de arquivo fica seco e não renderiza o site.
// Comparação de texto: procura o último ponto depois da última barra (o Path.HasExtension aceitaria ponto no fim).
static bool HasFileExtension(PathString path)
{
    var value = path.Value;
    if (string.IsNullOrEmpty(value))
        return false;

    var lastSlash = value.LastIndexOf('/');
    var lastDot = value.LastIndexOf('.');
    return lastDot > lastSlash && lastDot < value.Length - 1;
}

// Necessário para WebApplicationFactory<Program> nos testes de API
public partial class Program;