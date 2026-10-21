using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Web.Data;

/// <summary>
/// Captura somente-leitura para revisão visual e regressão: percorre as rotas do site (por papel) e do app
/// (pelo host de pré-visualização), em vários viewports e nos dois temas, e grava capturas, métricas por página
/// (metrics.js) e um resumo. Nada é confirmado nem excluído: diálogos e gavetas são só abertos e fechados.
/// </summary>
static class CaptureRunner
{
    const string Password = "Monburan123";

    static (string Name, int W, int H)[] WebViewports = [("desktop", 1440, 900), ("tablet", 834, 1112), ("mobile", 390, 844)];
    static readonly (string Name, int W, int H)[] AppViewports = [("small", 360, 800), ("medium", 390, 844), ("large", 412, 915)];

    sealed record Shot(string Role, string Name, string Path);

    static string[]? Roles;
    static string[]? Themes;
    static bool Wants(string role) => Roles is null || Roles.Contains(role);
    static IEnumerable<string> ThemesOr(params string[] all) => Themes is null ? all : all.Where(Themes.Contains);

    public static async Task<int> RunAsync(string[] args, string repoRoot, Func<string, MonburanApiFactoryHandle> startHost)
    {
        var mode = ArgValue(args, "--capture") ?? "all";
        // Recortes opcionais para rodar em blocos menores: --roles visitante,candidato --themes light
        Roles = ArgValue(args, "--roles")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Themes = ArgValue(args, "--themes")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        // --narrow: só 320px (fontes mais largas que as do Windows, como as do Linux, cabem com folga se 320px couber)
        if (args.Contains("--narrow"))
            WebViewports = [("narrow", 320, 700)];
        var outDir = ArgValue(args, "--out") ?? Path.Combine(Path.GetTempPath(), "monburan-capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(outDir);
        Console.WriteLine($"ARTIFACTS={outDir}");

        await using var host = startHost(Path.Combine(repoRoot, "src", "Monburan.Web"));
        Console.WriteLine($"HOST={host.BaseUrl}");
        var seed = await SeedAsync(host.Services);

        var metricsJs = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Capture", "metrics.js"));
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var session = new Session(browser, host.BaseUrl, outDir, metricsJs);

        if (mode is "all" or "web") await CaptureWebAsync(session, seed);
        if (mode is "all" or "mobile") await CaptureAppAsync(session, repoRoot, seed);

        session.Save();
        await File.WriteAllTextAsync(Path.Combine(outDir, "summary.txt"), Summarize(session.Results));
        Console.WriteLine($"DONE {session.Results.Count} -> {outDir}");
        return 0;
    }

    static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
    }

    sealed record Seed(Guid ResumeId, Guid AppliedJobId);

    // Fixture só no banco InMemory: currículo ativo e uma candidatura recebida pela empresa seedada
    static async Task<Seed> SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbf.CreateDbContextAsync();
        var candidate = await db.Candidates.FirstAsync(c => c.Email == "joao.silva@email.com");
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.CandidateId == candidate.Id && !r.IsDeleted);
        if (resume is null)
        {
            resume = new Resume { CandidateId = candidate.Id, Title = "Desenvolvedor .NET Pleno", IsActive = true, Summary = "Desenvolvedor com 5 anos de experiência em .NET, Blazor e PostgreSQL." };
            db.Resumes.Add(resume);
        }
        var advertiser = await db.Advertisers.FirstAsync(a => a.Email == "contato@techsolutions.com");
        var job = await db.Jobs.FirstAsync(j => j.AdvertiserId == advertiser.Id && !j.IsDeleted);
        await db.SaveChangesAsync();
        if (!await db.Applications.AnyAsync(a => a.CandidateId == candidate.Id && a.JobId == job.Id))
            db.Applications.Add(new Application { CandidateId = candidate.Id, JobId = job.Id, ResumeId = resume.Id, Status = EApplicationStatus.Sent, AppliedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return new Seed(resume.Id.Value, job.Id.Value);
    }

    sealed class Session(IBrowser browser, string baseUrl, string outDir, string metricsJs)
    {
        public List<Dictionary<string, object?>> Results { get; } = [];
        public string BaseUrl => baseUrl;
        public string OutDir => outDir;

        public async Task<IBrowserContext> NewContextAsync(string theme, string? themeKey = "monburan-theme")
        {
            var ctx = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 900 }, Locale = "pt-BR" });
            await ctx.RouteAsync("**/*", async r =>
            {
                var u = r.Request.Url;
                if (u.StartsWith("data:") || u.StartsWith("blob:") || (Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.IsLoopback)) await r.ContinueAsync();
                else await r.AbortAsync();
            });
            await ctx.AddInitScriptAsync($"try {{ localStorage.setItem('{themeKey}', '{theme}'); }} catch (e) {{}}");
            return ctx;
        }

        public async Task<JsonElement> MetricsAsync(IPage page) => await page.EvaluateAsync<JsonElement>(metricsJs);

        public async Task CaptureAsync(IBrowserContext ctx, string site, string role, string name, string url,
            (string Name, int W, int H)[] viewports, string theme, bool keyboard = false)
        {
            var page = await ctx.NewPageAsync();
            var console = new List<string>();
            page.Console += (_, m) => { if (m.Type is "error" or "warning") console.Add($"{m.Type}: {m.Text[..Math.Min(200, m.Text.Length)]}"); };
            page.PageError += (_, e) => console.Add($"pageerror: {e[..Math.Min(200, e.Length)]}");
            page.Response += (_, r) => { if (r.Status >= 400) console.Add($"http{r.Status}: {r.Url}"); };
            foreach (var vp in viewports)
            {
                await page.SetViewportSizeAsync(vp.W, vp.H);
                console.Clear();
                IResponse? resp = null;
                try { resp = await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 }); }
                catch (Exception ex) { console.Add("goto: " + ex.Message[..Math.Min(150, ex.Message.Length)]); }
                await page.WaitForTimeoutAsync(1500);
                var file = $"{site}__{role}__{name}__{vp.Name}__{theme}.png";
                // Uma tela que não deixa capturar não derruba a rodada: o erro entra no resultado
                try { await page.ScreenshotAsync(new() { Path = Path.Combine(outDir, file), FullPage = true, Timeout = 45000 }); }
                catch (TimeoutException ex) { console.Add("screenshot: " + ex.Message.Split('\n')[0]); file = string.Empty; }
                var row = new Dictionary<string, object?>
                {
                    ["site"] = site, ["role"] = role, ["page"] = name, ["path"] = url.Replace(baseUrl, ""), ["finalUrl"] = page.Url,
                    ["status"] = resp?.Status, ["viewport"] = $"{vp.W}x{vp.H}", ["theme"] = theme, ["shot"] = file,
                    ["metrics"] = await MetricsAsync(page), ["console"] = console.ToList()
                };
                if (keyboard && vp == viewports[0]) row["tabStops"] = await TabStopsAsync(page, 16);
                Results.Add(row);
                Console.WriteLine($"OK {site} {role} {name} {vp.Name} {theme}");
            }
            await page.CloseAsync();
        }

        public void Add(Dictionary<string, object?> row) => Results.Add(row);

        public void Save() => File.WriteAllText(Path.Combine(outDir, "results.json"), JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
    }

    const string FocusJs = "() => { const a = document.activeElement; if (!a || a === document.body) return null; const r = a.getBoundingClientRect(); const ring = [a, a.parentElement, a.closest('.job-card, .search-field, label')].filter(Boolean).some(n => { const s = getComputedStyle(n); return (s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0) || s.boxShadow !== 'none'; }); return { tag: a.tagName.toLowerCase(), text: (a.getAttribute('aria-label') || a.innerText || a.value || a.id || '').trim().replace(/\\s+/g, ' ').slice(0, 40), ring, inView: r.bottom > 0 && r.top < innerHeight && r.width > 0, inDialog: !!a.closest('dialog, [role=dialog]') }; }";

    static async Task<List<JsonElement>> TabStopsAsync(IPage page, int count)
    {
        await page.Mouse.ClickAsync(1, 1);
        var stops = new List<JsonElement>();
        for (var i = 0; i < count; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            stops.Add(await page.EvaluateAsync<JsonElement>(FocusJs));
        }
        return stops;
    }

    static async Task LoginWebAsync(IBrowserContext ctx, string baseUrl, string email)
    {
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(baseUrl + "/Account/Login");
        await page.Locator("[data-testid=login-email]").FillAsync(email);
        await page.Locator("[data-testid=login-password]").FillAsync(Password);
        await page.Locator("[data-testid=login-submit]").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Console.WriteLine($"LOGIN {email} -> {page.Url}");
        await page.CloseAsync();
    }

    static async Task<string?> FirstHrefAsync(IBrowserContext ctx, string url, string pattern)
    {
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(1200);
        var hrefs = await page.EvalOnSelectorAllAsync<string[]>("a[href]", "els => els.map(e => e.getAttribute('href'))");
        await page.CloseAsync();
        return hrefs.Select(h => Regex.Match(h ?? "", pattern)).FirstOrDefault(m => m.Success)?.Value;
    }

    // Envio vazio (ou próximo passo) para ver a validação: não grava nada porque o formulário é recusado
    static async Task FormProbeAsync(Session s, IBrowserContext ctx, string role, string name, string path, string submitSelector, string? clearSelector = null)
    {
        var page = await ctx.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync(s.BaseUrl + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(1500);
        if (clearSelector is not null && await page.Locator(clearSelector).CountAsync() > 0)
            await page.Locator(clearSelector).First.FillAsync("");
        var clicked = "no-button";
        var btn = page.Locator(submitSelector).First;
        if (await btn.CountAsync() > 0)
        {
            try { await btn.ClickAsync(new() { Timeout = 5000 }); clicked = "clicked"; }
            catch (Exception ex) { clicked = "click-failed: " + ex.GetType().Name; }
            await page.WaitForTimeoutAsync(1500);
        }
        var file = $"web__{role}__{name}__validation__mobile__light.png";
        await page.ScreenshotAsync(new() { Path = Path.Combine(s.OutDir, file), FullPage = true });
        var info = await page.EvaluateAsync<JsonElement>(@"() => ({
            url: location.pathname,
            messages: [...document.querySelectorAll('main .validation-message, main .validation-errors li, main [role=alert], main .alert-danger')].filter(e => e.offsetParent).map(e => e.innerText.trim()).filter(Boolean).slice(0, 12),
            ariaInvalid: document.querySelectorAll('main [aria-invalid=true]').length,
            focused: document.activeElement ? (document.activeElement.id || document.activeElement.tagName) : null })");
        s.Add(new() { ["site"] = "web", ["role"] = role, ["page"] = name + " (validação)", ["path"] = path, ["viewport"] = "390x844", ["shot"] = file, ["clicked"] = clicked, ["validation"] = info });
        await page.CloseAsync();
    }

    static async Task CaptureWebAsync(Session s, Seed seed)
    {
        var b = s.BaseUrl;
        var dark = WebViewports.Where(v => v.Name != "tablet").ToArray();

        async Task RoleAsync(string role, string? email, List<(string Name, string Path, bool Keyboard)> pages)
        {
            if (!Wants(role)) return;
            foreach (var theme in ThemesOr("light", "dark"))
            {
                var ctx = await s.NewContextAsync(theme);
                if (email is not null) await LoginWebAsync(ctx, b, email);
                foreach (var (name, path, keyboard) in pages)
                    await s.CaptureAsync(ctx, "web", role, name, b + path, theme == "light" ? WebViewports : dark, theme, keyboard && theme == "light");
                await ctx.CloseAsync();
            }
        }

        // ---------- Visitante ----------
        var probe = await s.NewContextAsync("light");
        var jobPath = await FirstHrefAsync(probe, b + "/jobs", @"^/jobs/[0-9a-f-]{36}$");
        var companyPath = jobPath is null ? null : await FirstHrefAsync(probe, b + jobPath, @"^/companies/[0-9a-f-]{36}$");
        await probe.CloseAsync();
        var visitor = new List<(string, string, bool)>
        {
            ("home", "/", true), ("jobs", "/jobs", true), ("jobs-filtered", "/jobs?q=desenvolvedor&modo=remoto", false), ("jobs-empty", "/jobs?q=zzzznaoexiste", false),
            ("plans", "/plans", false), ("terms", "/terms", false), ("privacy", "/privacy", false), ("404", "/rota-inexistente", false),
            ("login", "/Account/Login", true), ("register", "/Account/Register", false), ("forgot", "/Account/ForgotPassword", false),
            ("resend-confirmation", "/Account/ResendEmailConfirmation", false), ("lockout", "/Account/Lockout", false), ("access-denied", "/Account/AccessDenied", false),
            ("register-confirmation", "/Account/RegisterConfirmation?email=x%40y.com", false), ("forgot-confirmation", "/Account/ForgotPasswordConfirmation", false),
            ("reset-confirmation", "/Account/ResetPasswordConfirmation", false), ("invalid-reset", "/Account/InvalidPasswordReset", false), ("invalid-user", "/Account/InvalidUser", false),
            ("job-notfound", "/jobs/00000000-0000-0000-0000-000000000001", false), ("company-notfound", "/companies/00000000-0000-0000-0000-000000000001", false),
            ("protected-redirect", "/my-applications", false), ("error", "/Error", false),
        };
        if (jobPath is not null) visitor.Add(("job-details", jobPath, true));
        if (companyPath is not null) visitor.Add(("company", companyPath, false));
        await RoleAsync("visitante", null, visitor);
        if (Wants("visitante"))
        {
            var ctx = await s.NewContextAsync("light");
            await FormProbeAsync(s, ctx, "visitante", "login", "/Account/Login", "[data-testid=login-submit]");
            await FormProbeAsync(s, ctx, "visitante", "register", "/Account/Register", "main button[type=submit]");
            await FormProbeAsync(s, ctx, "visitante", "forgot", "/Account/ForgotPassword", "main button[type=submit]");
            await ctx.CloseAsync();
        }

        // ---------- Candidato ----------
        string? addressPath;
        {
            var ctx = await s.NewContextAsync("light");
            await LoginWebAsync(ctx, b, "joao.silva@email.com");
            addressPath = await FirstHrefAsync(ctx, b + "/address/list", @"^/address/edit/[0-9a-f-]{36}$");
            await ctx.CloseAsync();
        }
        var resumePath = "/resumes/" + seed.ResumeId;
        var candidate = new List<(string, string, bool)>
        {
            ("dashboard", "/", true), ("jobs", "/jobs", false), ("my-applications", "/my-applications", true), ("my-applications-saved", "/my-applications?aba=salvas", false),
            ("my-applications-alerts", "/my-applications?aba=alertas", false), ("career-profile", "/career-profile", false), ("profile", "/profile/candidate", false),
            ("resumes", "/resumes", false), ("resume-new", "/resumes/new", false), ("resume-wizard", "/resumes/wizard", false), ("resume-upload", "/resumes/upload", false),
            ("resume-details", resumePath, true), ("resume-edit", resumePath + "/edit", false), ("resume-cv", resumePath + "/cv", false), ("resume-wizard-edit", resumePath + "/wizard", false),
            ("notifications", "/notifications", false), ("address-list", "/address/list", false), ("address-create", "/address/create", false),
            ("address-edit-missing", "/address/edit/00000000-0000-0000-0000-000000000009", false), ("address-delete-missing", "/address/delete/00000000-0000-0000-0000-000000000009", false),
            ("manage-index", "/Account/Manage", false), ("manage-email", "/Account/Manage/Email", false), ("manage-password", "/Account/Manage/ChangePassword", false),
            ("manage-2fa", "/Account/Manage/TwoFactorAuthentication", false), ("manage-personal", "/Account/Manage/PersonalData", false),
            ("manage-external", "/Account/Manage/ExternalLogins", false), ("manage-enable-auth", "/Account/Manage/EnableAuthenticator", false),
            ("manage-reset-auth", "/Account/Manage/ResetAuthenticator", false), ("manage-delete-data", "/Account/Manage/DeletePersonalData", false),
            ("forbidden-advertiser-area", "/my-jobs", false), ("forbidden-admin-area", "/admin/accounts", false), ("plans", "/plans", false),
            ("apply-done", "/apply/" + seed.AppliedJobId, true),
        };
        if (jobPath is not null) candidate.Add(("job-details", jobPath, false));
        if (addressPath is not null) { candidate.Add(("address-edit", addressPath, false)); candidate.Add(("address-delete", addressPath.Replace("/edit/", "/delete/"), false)); }
        await RoleAsync("candidato", "joao.silva@email.com", candidate);
        if (Wants("candidato"))
        {
            var ctx = await s.NewContextAsync("light");
            await LoginWebAsync(ctx, b, "joao.silva@email.com");
            // Uma vaga ainda sem candidatura deste candidato: o formulário de candidatura (sem enviar)
            var openJob = await FirstHrefAsync(ctx, b + "/jobs", @"^/jobs/[0-9a-f-]{36}$");
            if (openJob is not null && !openJob.EndsWith(seed.AppliedJobId.ToString()))
                await s.CaptureAsync(ctx, "web", "candidato", "apply-form", b + "/apply/" + openJob.Split('/').Last(), WebViewports, "light");
            await FormProbeAsync(s, ctx, "candidato", "profile", "/profile/candidate", "main button[type=submit]", "#candidate-name");
            await FormProbeAsync(s, ctx, "candidato", "resume-wizard", "/resumes/wizard", "button:has-text('Próximo')");
            await FormProbeAsync(s, ctx, "candidato", "manage-password", "/Account/Manage/ChangePassword", "main button[type=submit]");
            await ctx.CloseAsync();
        }

        // ---------- Empresa ----------
        string? editPath, appsPath;
        {
            var ctx = await s.NewContextAsync("light");
            await LoginWebAsync(ctx, b, "contato@techsolutions.com");
            editPath = await FirstHrefAsync(ctx, b + "/my-jobs", @"^/jobs/edit/[0-9a-f-]{36}$");
            appsPath = await FirstHrefAsync(ctx, b + "/my-jobs", @"^/jobs/[0-9a-f-]{36}/applications$");
            await ctx.CloseAsync();
        }
        var advertiser = new List<(string, string, bool)>
        {
            ("dashboard", "/", false), ("my-jobs", "/my-jobs", true), ("received-applications", "/received-applications", true), ("job-create", "/jobs/create", true),
            ("profile", "/profile/advertiser", false), ("upload-logo", "/advertiser/upload-logo", false), ("subscription", "/my-subscription", false), ("plans", "/plans", false),
            ("notifications", "/notifications", false), ("forbidden-candidate-area", "/my-applications", false), ("jobs", "/jobs", false),
        };
        if (editPath is not null) advertiser.Add(("job-edit", editPath, false));
        if (appsPath is not null) advertiser.Add(("job-applications", appsPath, false));
        await RoleAsync("empresa", "contato@techsolutions.com", advertiser);
        if (Wants("empresa"))
        {
            var ctx = await s.NewContextAsync("light");
            await LoginWebAsync(ctx, b, "contato@techsolutions.com");
            await FormProbeAsync(s, ctx, "empresa", "job-create", "/jobs/create", "button:has-text('Próximo'), button:has-text('Continuar')");
            await OverlaysAsync(s, ctx);
            await ctx.CloseAsync();
        }

        // ---------- Admin ----------
        var admin = new List<(string, string, bool)>
        {
            ("dashboard", "/", false), ("accounts", "/admin/accounts", true), ("jobs", "/admin/jobs", false), ("audit", "/admin/audit", false),
            ("ai-providers", "/admin/ai-providers", false), ("candidates", "/admin/candidates", false), ("advertisers", "/admin/advertisers", false),
        };
        await RoleAsync("admin", "admin@monburan.com.br", admin);
    }

    // Gaveta do candidato, menu de ações e diálogo de exclusão: abrir, percorrer com Tab e fechar com Esc (sem confirmar)
    static async Task OverlaysAsync(Session s, IBrowserContext ctx)
    {
        var page = await ctx.NewPageAsync();
        foreach (var vp in WebViewports.Length > 1 ? new[] { WebViewports[0], WebViewports[^1] } : WebViewports)
        {
            await page.SetViewportSizeAsync(vp.W, vp.H);
            await page.GotoAsync(s.BaseUrl + "/received-applications", new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForTimeoutAsync(1500);
            var open = page.Locator("[data-testid=applicant-open], button:has-text('Perfil e currículo')").First;
            if (await open.CountAsync() == 0 || !await open.IsVisibleAsync()) continue;
            await open.ClickAsync();
            await page.WaitForTimeoutAsync(1200);
            await page.ScreenshotAsync(new() { Path = Path.Combine(s.OutDir, $"web__empresa__drawer__{vp.Name}__light.png") });
            var stops = new List<JsonElement>();
            for (var i = 0; i < 12; i++) { await page.Keyboard.PressAsync("Tab"); stops.Add(await page.EvaluateAsync<JsonElement>(FocusJs)); }
            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(500);
            s.Add(new() { ["site"] = "web", ["role"] = "empresa", ["page"] = "gaveta do candidato", ["viewport"] = $"{vp.W}x{vp.H}", ["tabStops"] = stops, ["afterEscape"] = await page.EvaluateAsync<JsonElement>(FocusJs) });
        }
        await page.SetViewportSizeAsync(1440, 900);
        await page.GotoAsync(s.BaseUrl + "/my-jobs", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(1500);
        var menu = page.Locator("button[id^=job-actions-]").First;
        if (await menu.CountAsync() > 0)
        {
            await menu.ClickAsync();
            await page.WaitForTimeoutAsync(400);
            await page.ScreenshotAsync(new() { Path = Path.Combine(s.OutDir, "web__empresa__my-jobs-menu__desktop__light.png") });
            var del = page.Locator("[data-testid=my-job-delete]").First;
            if (await del.CountAsync() > 0)
            {
                await del.ClickAsync();
                await page.WaitForTimeoutAsync(800);
                await page.ScreenshotAsync(new() { Path = Path.Combine(s.OutDir, "web__empresa__confirm-dialog__desktop__light.png") });
                var stops = new List<JsonElement>();
                for (var i = 0; i < 6; i++) { await page.Keyboard.PressAsync("Tab"); stops.Add(await page.EvaluateAsync<JsonElement>(FocusJs)); }
                await page.Keyboard.PressAsync("Escape");
                await page.WaitForTimeoutAsync(500);
                s.Add(new() { ["site"] = "web", ["role"] = "empresa", ["page"] = "diálogo de exclusão", ["viewport"] = "1440x900", ["tabStops"] = stops, ["afterEscape"] = await page.EvaluateAsync<JsonElement>(FocusJs), ["dialogStillOpen"] = await page.Locator("dialog[open]").CountAsync() });
            }
        }
        await page.CloseAsync();
    }

    // ---------- App (host de pré-visualização) ----------
    static async Task CaptureAppAsync(Session s, string repoRoot, Seed seed)
    {
        var exe = Path.Combine(repoRoot, "tests", "ux-mobile-preview", "bin", "Debug", "net10.0", OperatingSystem.IsWindows() ? "ux-mobile-preview.exe" : "ux-mobile-preview");
        if (!File.Exists(exe))
        {
            Console.WriteLine($"APP_SKIPPED: compile tests/ux-mobile-preview antes ({exe})");
            return;
        }

        var jobs = new List<(string, string)>
        {
            ("home", "/"), ("jobs", "/jobs"), ("job-details", "/jobs/" + seed.AppliedJobId), ("login", "/login"), ("register", "/register"),
            ("forgot-password", "/forgot-password"), ("not-found", "/not-found"), ("protected", "/applications"),
        };
        var candidate = new List<(string, string)>
        {
            ("home", "/"), ("jobs", "/jobs"), ("job-details", "/jobs/" + seed.AppliedJobId), ("apply-done", "/apply/" + seed.AppliedJobId),
            ("applications", "/applications"), ("saved-jobs", "/applications?aba=salvas"), ("job-alerts", "/applications?aba=alertas"),
            ("notifications", "/notifications"), ("profile", "/profile"), ("career-profile", "/career-profile"), ("plans", "/plans"),
            ("resumes", "/resumes"), ("resume-details", "/resumes/" + seed.ResumeId), ("resume-edit", "/resumes/" + seed.ResumeId + "/edit"),
            ("resume-new", "/resumes/new"),
        };

        // Uma pessoa por processo (as sessões do app são singletons): visitante e candidato em processos separados
        foreach (var (role, email, pages) in new[] { ("visitante", (string?)null, jobs), ("candidato", "joao.silva@email.com", candidate) })
        {
            if (!Wants(role)) continue;
            await using var preview = await PreviewProcess.StartAsync(exe, s.BaseUrl + "/");
            foreach (var theme in ThemesOr("dark", "light"))
            {
                var ctx = await s.NewContextAsync(theme);
                // A sessão do app vive no processo: no segundo tema a pessoa já está logada e /login redireciona
                if (email is not null)
                {
                    var p = await ctx.NewPageAsync();
                    await p.GotoAsync(preview.Url + "/login", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await p.WaitForTimeoutAsync(1500);
                    if (await p.Locator("#login-email").CountAsync() == 0) { await p.CloseAsync(); goto Logged; }
                    await p.Locator("#login-email").FillAsync(email);
                    await p.Locator("#login-password").FillAsync(Password);
                    await p.Locator("form button[type=submit]").ClickAsync();
                    await p.WaitForTimeoutAsync(3000);
                    Console.WriteLine($"APP LOGIN {email} -> {p.Url}");
                    await p.CloseAsync();
                }
            Logged:
                foreach (var (name, path) in pages)
                    await s.CaptureAsync(ctx, "app", role, name, preview.Url + path, theme == "dark" ? AppViewports : [AppViewports[1]], theme, keyboard: theme == "dark");
                await ctx.CloseAsync();
            }
        }
    }

    sealed class PreviewProcess : IAsyncDisposable
    {
        private readonly Process _process;
        public string Url { get; }

        private PreviewProcess(Process process, string url) { _process = process; Url = url; }

        public static async Task<PreviewProcess> StartAsync(string exe, string apiBase)
        {
            var port = FreePort();
            var url = $"http://127.0.0.1:{port}";
            var psi = new ProcessStartInfo(exe, ["--api", apiBase, "--urls", url, "--environment", "Development"])
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe)!
            };
            var process = Process.Start(psi) ?? throw new InvalidOperationException("Pré-visualização do app não iniciou");
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine("PREVIEW_ERR " + e.Data); };
            process.BeginErrorReadLine();
            using var http = new HttpClient();
            for (var i = 0; i < 60; i++)
            {
                try { if ((await http.GetAsync(url + "/app.css")).IsSuccessStatusCode) return new PreviewProcess(process, url); }
                catch (HttpRequestException) { }
                await Task.Delay(1000);
            }
            process.Kill(true);
            throw new TimeoutException("Pré-visualização do app não respondeu em 60 s");
        }

        static int FreePort()
        {
            var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public ValueTask DisposeAsync()
        {
            if (!_process.HasExited) _process.Kill(true);
            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    // ---------- Resumo ----------
    static string Summarize(List<Dictionary<string, object?>> rows)
    {
        static JsonElement M(Dictionary<string, object?> r) => (JsonElement)r["metrics"]!;
        var pages = rows.Where(r => r.ContainsKey("metrics")).ToList();
        var o = new List<string> { $"capturas={pages.Count} paginas_unicas={pages.Select(r => $"{r["site"]}:{r["role"]}:{r["page"]}").Distinct().Count()}" };

        o.Add("\n== Status, h1, título (primeiro viewport, tema claro ou escuro do app) ==");
        foreach (var r in pages.GroupBy(r => $"{r["site"]}:{r["role"]}:{r["page"]}").Select(g => g.First()))
        {
            var m = M(r);
            o.Add($"{r["site"]}/{r["role"]}/{r["page"]} {r["status"]} {r["path"]} -> {new Uri((string)r["finalUrl"]!).PathAndQuery} | h1={m.GetProperty("h1")} | title=\"{m.GetProperty("title")}\" | skips={m.GetProperty("headingSkips")} | eng={m.GetProperty("englishHits")}");
        }

        void Section(string title, Func<JsonElement, bool> when, Func<Dictionary<string, object?>, JsonElement, string> line)
        {
            o.Add($"\n== {title} ==");
            foreach (var r in pages) { var m = M(r); if (when(m)) o.Add($"{r["site"]}/{r["role"]}/{r["page"]} {r["viewport"]} {r["theme"]} {line(r, m)}"); }
        }
        Section("Overflow horizontal", m => m.GetProperty("horizontalOverflow").GetInt32() > 0, (_, m) => $"+{m.GetProperty("horizontalOverflow")}px {m.GetProperty("overflowEls")}");
        Section("Contraste abaixo de AA", m => m.GetProperty("lowContrastCount").GetInt32() > 0, (_, m) => $"n={m.GetProperty("lowContrastCount")} {m.GetProperty("lowContrast")}");
        Section("Texto < 12px", m => m.GetProperty("smallTextCount").GetInt32() > 0, (_, m) => $"n={m.GetProperty("smallTextCount")} {m.GetProperty("smallText")}");
        Section("Alvos < 24px (WCAG 2.5.8)", m => m.GetProperty("smallTargetCount").GetInt32() > 0, (_, m) => $"n={m.GetProperty("smallTargetCount")} {m.GetProperty("smallTargets")}");
        Section("Campos sem rótulo / controles sem nome / ids duplicados / img sem alt", m => m.GetProperty("unlabeled").GetArrayLength() + m.GetProperty("nameless").GetArrayLength() + m.GetProperty("dupIds").GetArrayLength() + m.GetProperty("imgNoAlt").GetInt32() > 0,
            (_, m) => $"unl={m.GetProperty("unlabeled")} noname={m.GetProperty("nameless")} dup={m.GetProperty("dupIds")} img={m.GetProperty("imgNoAlt")}");
        Section("Sem h1 ou com mais de um", m => m.GetProperty("h1").GetArrayLength() != 1, (_, m) => $"h1={m.GetProperty("h1")}");
        Section("Erro do Blazor visível", m => m.GetProperty("errorUiVisible").GetBoolean(), (_, _) => "");

        o.Add("\n== Console / HTTP >= 400 (sem duplicatas) ==");
        var cons = new Dictionary<string, HashSet<string>>();
        foreach (var r in pages)
            foreach (var c in (List<string>)r["console"]!)
            {
                var k = Regex.Replace(Regex.Replace(c, @"http://127\.0\.0\.1:\d+", ""), @"[0-9a-f]{8}-[0-9a-f-]{27}", "{id}");
                if (!cons.TryGetValue(k, out var set)) cons[k] = set = [];
                set.Add($"{r["site"]}/{r["role"]}/{r["page"]}");
            }
        foreach (var (k, v) in cons) o.Add($"{k} :: {string.Join(", ", v.Take(6))}{(v.Count > 6 ? $" +{v.Count - 6}" : "")}");

        o.Add("\n== Teclado (paradas de Tab; !FOCO = sem anel visível, !FORA = fora da tela) ==");
        foreach (var r in rows.Where(r => r.ContainsKey("tabStops")))
        {
            var stops = ((List<JsonElement>)r["tabStops"]!).Select(e => e.ValueKind == JsonValueKind.Null ? "body" :
                $"{e.GetProperty("tag")}[{e.GetProperty("text").GetString()![..Math.Min(18, e.GetProperty("text").GetString()!.Length)]}]{(e.GetProperty("ring").GetBoolean() ? "" : "!FOCO")}{(e.GetProperty("inView").GetBoolean() ? "" : "!FORA")}");
            o.Add($"{r["site"]}/{r["role"]}/{r["page"]} {r["viewport"]}: {string.Join(" > ", stops)}{(r.TryGetValue("afterEscape", out var after) ? $" || após Esc={after}" : "")}{(r.TryGetValue("dialogStillOpen", out var still) ? $" aberto={still}" : "")}");
        }

        o.Add("\n== Validação ==");
        foreach (var r in rows.Where(r => r.ContainsKey("validation"))) o.Add($"{r["site"]}/{r["role"]}/{r["page"]} [{r["clicked"]}]: {r["validation"]}");
        return string.Join('\n', o);
    }
}

/// <summary>Host InMemory do site com o endereço e os serviços que a captura usa.</summary>
sealed class MonburanApiFactoryHandle(IAsyncDisposable owner, string baseUrl, IServiceProvider services) : IAsyncDisposable
{
    public string BaseUrl { get; } = baseUrl;
    public IServiceProvider Services { get; } = services;
    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
