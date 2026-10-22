using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Monburan.ApiTests;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Web.Data;

// Harness isolado (fora da solução/CI): sobe o app com MonburanApiFactory
// (EF InMemory + FakeAiTextClient) em Kestrel loopback e valida o fluxo de UX
// no navegador. Não é credencial real: chaves de teste só para o host arrancar.
Environment.SetEnvironmentVariable("Jwt__SigningKey", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
Environment.SetEnvironmentVariable("Jwt__Issuer", "monburan-ux-smoke");
Environment.SetEnvironmentVariable("Jwt__Audience", "monburan-ux-smoke");

// Raiz do repositório descoberta subindo diretórios até achar Monburan.sln
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Monburan.sln")))
        dir = dir.Parent;
    if (dir is null)
    {
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Monburan.sln")))
            dir = dir.Parent;
    }
    return dir?.FullName ?? throw new DirectoryNotFoundException("Monburan.sln não encontrado");
}

static void Check(bool condition, string name)
{
    Console.WriteLine($"{name}:{condition}");
    if (!condition) throw new InvalidOperationException($"UX-SMOKE FALHOU: {name}");
}

// Só loopback + data/blob: qualquer chamada externa é abortada
static async Task LoopbackOnly(Microsoft.Playwright.IRoute route)
{
    var url = route.Request.Url;
    if (url.StartsWith("data:") || url.StartsWith("blob:"))
    {
        await route.ContinueAsync();
        return;
    }
    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsLoopback)
        await route.ContinueAsync();
    else
        await route.AbortAsync();
}

// Modo de captura (revisão visual e regressão): ver Capture/CaptureRunner.cs e o README
if (args.Contains("--notifications"))
{
    await NotificationChecks.RunAsync(FindRepoRoot());
    return;
}

if (args.Contains("--capture"))
{
    Environment.ExitCode = await CaptureRunner.RunAsync(args, FindRepoRoot(), contentRoot =>
    {
        var captureFactory = new UxFactory(contentRoot);
        captureFactory.UseKestrel(0);
        var captureClient = captureFactory.CreateClient();
        return new MonburanApiFactoryHandle(captureFactory, captureClient.BaseAddress!.ToString().TrimEnd('/'), captureFactory.Services);
    });
    return;
}

if (args.Contains("--serve-only"))
{
    await using var testHost = new UxFactory(Path.Combine(FindRepoRoot(), "src", "Monburan.Web"));
    testHost.UseKestrel(0);
    using var testClient = testHost.CreateClient();
    Console.WriteLine($"HOST={testClient.BaseAddress!.ToString().TrimEnd('/')}");
    await Task.Delay(Timeout.Infinite);
    return;
}

var repoRoot = FindRepoRoot();
var outputDir = Path.Combine(Path.GetTempPath(), "monburan-ux-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outputDir);
Console.WriteLine($"ARTIFACTS={outputDir}");

await using var factory = new UxFactory(Path.Combine(repoRoot, "src", "Monburan.Web"));
try
{
    factory.UseKestrel(0);
    var client = factory.CreateClient();
    var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');
    Console.WriteLine($"HOST={baseUrl}");
    var health = await client.GetAsync("/health");
    Check(health.IsSuccessStatusCode, "HEALTH");

    var fakeAi = factory.Services.GetRequiredService<FakeAiTextClient>();
    using var scope = factory.Services.CreateScope();
    var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    Guid resumeId;
    {
        await using var db = await dbf.CreateDbContextAsync();
        var candidate = await db.Candidates.FirstAsync(c => c.Email == "joao.silva@email.com");
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.CandidateId == candidate.Id && !r.IsDeleted);
        if (resume is null)
        {
            resume = new Resume { CandidateId = candidate.Id, Title = "Currículo E2E", IsActive = true, Summary = "Resumo profissional original." };
            db.Resumes.Add(resume);
        }
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.HasAiAssistant);
        if (plan is not null && !await db.Subscriptions.AnyAsync(x => x.CandidateId == candidate.Id && x.Status == ESubscriptionStatus.Active))
            db.Subscriptions.Add(new Subscription { CandidateId = candidate.Id, PlanId = plan.Id, Status = ESubscriptionStatus.Active, StartDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        resumeId = resume.Id.Value;
        Console.WriteLine($"SEED_RESUME={resumeId} PLAN={(plan is null ? "none" : "ok")}");
    }

    using var pw = await Playwright.CreateAsync();
    await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
    var ctx = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true, BypassCSP = true });
    await ctx.RouteAsync("**/*", LoopbackOnly);
    var page = await ctx.NewPageAsync();
    page.SetDefaultTimeout(45000);

    // Anônimo: sem sidebar, scrim ou botão de menu — navegação pública no topo
    await page.GotoAsync(baseUrl + "/jobs");
    await page.WaitForTimeoutAsync(2500);
    Check(await page.Locator(".app-sidebar").CountAsync() == 0, "ANON_NO_SIDEBAR");
    Check(await page.Locator(".sidebar-scrim").CountAsync() == 0, "ANON_NO_SCRIM");
    Check(await page.Locator("[data-testid=mobile-menu-toggle]").CountAsync() == 0, "ANON_NO_MENU_BTN");
    Check(await page.Locator(".public-header").CountAsync() == 1, "ANON_PUBLIC_HEADER");
    var stageLeft = await page.EvaluateAsync<double>("() => document.querySelector('.app-stage').getBoundingClientRect().left");
    Check(stageLeft <= 1, "ANON_NO_GUTTER_1440");
    await page.SetViewportSizeAsync(390, 844);
    var stageLeft390 = await page.EvaluateAsync<double>("() => document.querySelector('.app-stage').getBoundingClientRect().left");
    var swAnon = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
    Check(stageLeft390 <= 1 && swAnon <= 390, "ANON_FITS_390");
    await page.SetViewportSizeAsync(1440, 900);

    // login candidato seedado (mesmas credenciais de desenvolvimento dos testes E2E)
    await page.GotoAsync(baseUrl + "/Account/Login");
    await page.WaitForTimeoutAsync(3000);
    await page.Locator("[data-testid=login-email]").FillAsync("joao.silva@email.com");
    await page.Locator("[data-testid=login-password]").FillAsync("Monburan123");
    await page.Locator("[data-testid=login-submit]").ClickAsync();
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    Console.WriteLine($"LOGIN_URL={page.Url}");

    // gera uma vez; o rascunho vira arquivo no navegador
    await page.GotoAsync($"{baseUrl}/resumes/{resumeId}?melhorar=true");
    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    await page.WaitForTimeoutAsync(3000);
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "detail-panel.png"), FullPage = true });
    var genBtn = page.GetByTestId("resume-ai-generate");
    Check(await genBtn.CountAsync() > 0, "GENERATION_AVAILABLE");
    Check(await page.GetByTestId("resume-ai-consent").CountAsync() == 0, "NO_CONSENT_CHECKBOX");
    Check(await genBtn.IsEnabledAsync(), "GENERATION_READY");
    Check(fakeAi.Requests.Count == 0, "NO_AUTOMATIC_AI");
    await genBtn.ClickAsync();
    await page.GetByTestId("resume-ai-draft").WaitForAsync(new() { Timeout = 90000 });
    Check(fakeAi.Requests.Count == 1, "SMOKE=DRAFT_CREATED");
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "draft-created.png"), FullPage = true });

    // recarrega: rascunho restaura sem nova chamada de IA
    await page.GotoAsync($"{baseUrl}/resumes/{resumeId}?melhorar=true");
    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    await page.WaitForTimeoutAsync(3000);
    await page.GetByTestId("resume-ai-review").WaitForAsync();
    await page.GetByTestId("resume-ai-discard").WaitForAsync();
    Check(fakeAi.Requests.Count == 1, "SMOKE=DRAFT_RESTORED");
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "draft-restored.png"), FullPage = true });

    // IA desligada agora: o rascunho salvo continua revisável (sem nova chamada)
    fakeAi.IsConfigured = false;
    await page.GotoAsync($"{baseUrl}/resumes/{resumeId}?melhorar=true");
    await page.WaitForTimeoutAsync(3000);
    await page.GetByTestId("resume-ai-review").WaitForAsync();
    Check(fakeAi.Requests.Count == 1, "SMOKE=AI_OFF_REVIEWABLE");
    fakeAi.IsConfigured = true;

    await page.GetByTestId("resume-ai-review").ClickAsync();
    await page.GetByTestId("resume-ai-workspace").WaitForAsync();
    Console.WriteLine("SMOKE=WORKSPACE_OPEN calls=" + fakeAi.Requests.Count);
    Check(fakeAi.Requests.Count == 1, "AI_SINGLE_CALL");
    await page.SetViewportSizeAsync(1440, 900);
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "workspace-1440.png"), FullPage = true });
    await page.SetViewportSizeAsync(390, 844);
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "workspace-390.png"), FullPage = true });
    await page.SetViewportSizeAsync(1440, 900);

    // seleciona resumo, edita sem blur e confere gravação ao vivo
    var edited = "Resumo editado pelo fumo E2E sem clique fora";
    var summaryCheck = page.GetByTestId("ai-select-summary");
    await page.Mouse.MoveAsync(900, 400); // tira o ponteiro da rail: sidebar recolhe
    await page.WaitForTimeoutAsync(400);
    await summaryCheck.CheckAsync(); // sem force/eval: valida a checkbox de verdade
    await summaryCheck.PressAsync(" ");
    await summaryCheck.CheckAsync(); // espaço desmarcou; marca de novo
    await page.GetByTestId("ai-edit-summary-text").FillAsync(edited);
    await page.GetByTestId("ai-save-status").Filter(new() { HasText = "salvo" }).WaitForAsync(new() { Timeout = 15000 });
    Console.WriteLine("SMOKE=SAVED_LIVE");

    // recarrega sem blur: rascunho restaura com a edição
    await page.GotoAsync($"{baseUrl}/resumes/{resumeId}?melhorar=true");
    await page.WaitForTimeoutAsync(3000);
    await page.GetByTestId("resume-ai-review").ClickAsync();
    await page.GetByTestId("ai-edit-summary-text").WaitForAsync();
    var restoredText = await page.GetByTestId("ai-edit-summary-text").InputValueAsync();
    Check(restoredText == edited, "SMOKE=EDIT_RESTORED");

    // prévia mostra o texto selecionado
    await page.GetByTestId("ai-workspace-section-previa").ClickAsync();
    var previewText = await page.Locator(".cv-sheet:visible").InnerTextAsync();
    Check(previewText.Contains(edited), "SMOKE=PREVIEW_HAS_EDIT");

    // aplica: grava no servidor e some com o rascunho
    await page.GetByTestId("ai-apply").ClickAsync();
    await page.WaitForTimeoutAsync(4000);
    {
        await using var db2 = await dbf.CreateDbContextAsync();
        var after = await db2.Resumes.AsNoTracking().FirstAsync(r => r.Id == Monburan.Core.ValueObjects.Id.Create(resumeId));
        Check(after.Summary == edited, "SMOKE=APPLIED");
        Check(after.Title == "Currículo E2E", "TITLE_KEPT");
    }
    Check(await page.GetByTestId("resume-ai-draft").CountAsync() == 0, "DRAFT_GONE");

    // overflow horizontal em 390px
    await page.SetViewportSizeAsync(390, 844);
    var scrollW = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
    var innerW = await page.EvaluateAsync<int>("() => window.innerWidth");
    Check(scrollW <= innerW, $"OVERFLOW_390 scrollWidth={scrollW} inner={innerW}");
    await page.SetViewportSizeAsync(1440, 900);
    await page.EvaluateAsync("() => document.documentElement.dataset.theme = 'dark'");
    await page.WaitForTimeoutAsync(500);
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "detail-dark-1440.png"), FullPage = false });
    await page.EvaluateAsync("() => document.documentElement.dataset.theme = 'light'");
    await page.WaitForTimeoutAsync(300);

    // impressão: CV isolado e PDF com conteúdo
    await page.GotoAsync($"{baseUrl}/resumes/{resumeId}/cv");
    await page.WaitForTimeoutAsync(3000);
    await page.EmulateMediaAsync(new() { Media = Media.Print });
    await page.WaitForFunctionAsync("""
        () => {
            const toolbar = document.querySelector('[data-testid="cv-toolbar"]');
            const sheet = document.querySelector('.cv-print-area .cv-sheet');
            return toolbar && sheet
                && getComputedStyle(toolbar).display === 'none'
                && getComputedStyle(sheet).fontFamily.includes('Georgia');
        }
        """);
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "cv-print-media.png"), FullPage = true });
    await page.PdfAsync(new() { Path = Path.Combine(outputDir, "cv.pdf"), Format = "A4" });
    await page.EmulateMediaAsync(new() { Media = Media.Screen }); // volta à tela: o print CSS fica só na impressão

    // --- Recrutador: lê só o currículo anexado; outro currículo é negado ---
    Guid otherResumeId;
    {
        await using var db3 = await dbf.CreateDbContextAsync();
        var advertiser = await db3.Advertisers.FirstAsync(a => a.Email == "contato@techsolutions.com");
        var job = await db3.Jobs.FirstAsync(j => j.AdvertiserId == advertiser.Id && !j.IsDeleted);
        var candidate = await db3.Candidates.FirstAsync(c => c.Email == "joao.silva@email.com");
        db3.Applications.Add(new Application
        {
            CandidateId = candidate.Id, JobId = job.Id, ResumeId = resumeId,
            Status = EApplicationStatus.Sent, AppliedAt = DateTime.UtcNow
        });
        var other = new Resume { CandidateId = candidate.Id, Title = "CV não enviado", IsActive = true };
        db3.Resumes.Add(other);
        await db3.SaveChangesAsync();
        otherResumeId = other.Id.Value;
    }

    var ctx2 = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true, BypassCSP = true });
    await ctx2.RouteAsync("**/*", LoopbackOnly);
    var rec = await ctx2.NewPageAsync();
    rec.SetDefaultTimeout(45000);
    await rec.GotoAsync(baseUrl + "/Account/Login");
    await rec.WaitForTimeoutAsync(2500);
    await rec.Locator("[data-testid=login-email]").FillAsync("contato@techsolutions.com");
    await rec.Locator("[data-testid=login-password]").FillAsync("Monburan123");
    await rec.Locator("[data-testid=login-submit]").ClickAsync();
    await rec.WaitForTimeoutAsync(3000);

    await rec.GotoAsync($"{baseUrl}/resumes/{resumeId}");
    await rec.WaitForTimeoutAsync(3000);
    var recDetail = await rec.ContentAsync();
    Check(recDetail.Contains("Currículo E2E"), "RECRUITER_VIEW");
    Check(!recDetail.Contains("Editar currículo") && !recDetail.Contains("Melhorar currículo") && !recDetail.Contains("resume-delete"), "READONLY");
    await rec.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "recruiter-resume-1440.png"), FullPage = true });
    await rec.SetViewportSizeAsync(390, 844);
    await rec.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "recruiter-resume-390.png"), FullPage = true });
    await rec.SetViewportSizeAsync(1440, 900);

    await rec.GotoAsync($"{baseUrl}/resumes/{resumeId}/cv");
    await rec.WaitForTimeoutAsync(3000);
    var recCv = await rec.ContentAsync();
    Check(recCv.Contains("cv-sheet"), "RECRUITER_CV");
    Check(!recCv.Contains("Editar currículo"), "NO_EDIT");
    Check(recCv.Contains("Imprimir"), "PRINT");
    await rec.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "recruiter-cv.png"), FullPage = true });

    await rec.GotoAsync($"{baseUrl}/resumes/{otherResumeId}");
    await rec.WaitForTimeoutAsync(3000);
    var denied = await rec.ContentAsync();
    Check(denied.Contains("não encontrado") || denied.Contains("Currículo não encontrado"), "RECRUITER_IDOR_DENIED");
    await ctx2.CloseAsync();

    // --- Perfil: AddressId preservado em desfazer e salvar ---
    {
        await using var db4 = await dbf.CreateDbContextAsync();
        var candidate = await db4.Candidates.FirstAsync(c => c.Email == "joao.silva@email.com");
        if (candidate.AddressId is null)
        {
            var address = new Address { City = "São Paulo", State = "SP", Street = "Rua X", Number = "1", ZipCode = "01000-000" };
            db4.Addresses.Add(address);
            candidate.AddressId = address.Id;
            await db4.SaveChangesAsync();
        }
    }
    await page.GotoAsync(baseUrl + "/profile/candidate");
    await page.WaitForTimeoutAsync(3000);
    await page.ScreenshotAsync(new() { Path = Path.Combine(outputDir, "profile.png"), FullPage = true });
    await page.GetByTestId("candidate-profile-name").FillAsync("João Silva Editado");
    await page.Mouse.MoveAsync(900, 400);
    await page.WaitForTimeoutAsync(400);
    await page.Locator("button:has-text('Desfazer alterações'):visible").ClickAsync();
    await page.WaitForTimeoutAsync(1000);
    await page.GetByTestId("candidate-profile-name").FillAsync("João Silva E2E");
    await page.Mouse.MoveAsync(900, 400);
    await page.WaitForTimeoutAsync(400);
    await page.GetByRole(AriaRole.Button, new() { Name = "Salvar alterações" }).ClickAsync();
    await page.WaitForTimeoutAsync(3000);
    {
        await using var db5 = await dbf.CreateDbContextAsync();
        var candidate = await db5.Candidates.AsNoTracking().FirstAsync(c => c.Email == "joao.silva@email.com");
        Check(candidate.AddressId is not null, "PROFILE_ADDRESS_PRESERVED");
        Check(candidate.Name == "João Silva E2E", "NAME_SAVED");
    }

    // --- Vaga já candidatada: detalhe mostra acompanhamento e /apply barra ---
    {
        await using var db6 = await dbf.CreateDbContextAsync();
        var applied = await db6.Applications
            .Where(a => a.ResumeId == resumeId && !a.IsDeleted)
            .FirstAsync();
        var jobId = applied.JobId.Value;

        await page.GotoAsync($"{baseUrl}/jobs/{jobId}");
        await page.WaitForTimeoutAsync(3000);
        var detailHtml = await page.ContentAsync();
        var applyCount = await page.GetByTestId("job-apply").CountAsync();
        Check(detailHtml.Contains("Acompanhar candidatura"), "DUP_DETAIL_TRACK");
        Check(applyCount == 0, "NO_APPLY");

        await page.GotoAsync($"{baseUrl}/apply/{jobId}");
        await page.WaitForTimeoutAsync(3000);
        var applyHtml = await page.ContentAsync();
        var submitCount = await page.GetByTestId("application-submit").CountAsync();
        Check(applyHtml.Contains("já se candidatou"), "DUP_APPLY_BLOCKED");
        Check(submitCount == 0, "NO_SUBMIT");

        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync($"{baseUrl}/jobs/{jobId}");
        await page.WaitForTimeoutAsync(3000);
        var mobileHtml = await page.ContentAsync();
        Check(mobileHtml.Contains("job-track-application-mobile"), "DUP_MOBILE_TRACK");
        await page.SetViewportSizeAsync(1440, 900);
    }

    // --- CV com 2+ páginas: conteúdo longo não fica em branco nem cortado ---
    {
        await using var db7 = await dbf.CreateDbContextAsync();
        var resume = await db7.Resumes.Include(r => r.ExperienceHistory).FirstAsync(r => r.Id == Monburan.Core.ValueObjects.Id.Create(resumeId));
        var lines = string.Join("\n\n", Enumerable.Range(1, 120).Select(i => $"LINHA PROFISSIONAL NUMERO {i:000} — realização comprovada com métricas."));
        resume.ExperienceHistory.Add(new Experience
        {
            Company = "ACME", Position = "Analista",
            Description = lines, StartDate = new DateTime(2020, 1, 1), IsCurrent = true
        });
        await db7.SaveChangesAsync();

        await page.GotoAsync($"{baseUrl}/resumes/{resumeId}/cv");
        await page.WaitForTimeoutAsync(3000);
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        await page.WaitForFunctionAsync("""
            () => {
                const toolbar = document.querySelector('[data-testid="cv-toolbar"]');
                const sheet = document.querySelector('.cv-print-area .cv-sheet');
                return toolbar && sheet
                    && getComputedStyle(toolbar).display === 'none'
                    && getComputedStyle(sheet).fontFamily.includes('Georgia');
            }
            """);
        await page.PdfAsync(new() { Path = Path.Combine(outputDir, "cv-long.pdf"), Format = "A4" });
        await page.EmulateMediaAsync(new() { Media = Media.Screen });

        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(Path.Combine(outputDir, "cv-long.pdf")))
        {
            var allText = string.Join("\n", pdf.GetPages().Select(p => p.Text));
            Check(pdf.NumberOfPages >= 2, $"CV_PAGES={pdf.NumberOfPages}");
            Check(allText.Contains("NUMERO 001"), "FIRST_LINE");
            Check(allText.Contains("NUMERO 120"), "LAST_LINE");
            Check(!allText.Contains("Monburan"), "NO_NAV");
        }
    }

    Console.WriteLine("SMOKE=DONE");
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL={ex.GetType().Name}: {ex.Message[..Math.Min(400, ex.Message.Length)]}");
    Environment.ExitCode = 2;
}
if (args.Contains("--serve") && Environment.ExitCode == 0)
{
    Console.WriteLine("HOST_STAYS_ALIVE");
    while (true) await Task.Delay(60000);
}

// Factory com content root explícito no projeto Web (sem truque de solução)
sealed class UxFactory(string contentRoot) : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseContentRoot(contentRoot);
    }
}
