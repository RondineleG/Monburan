using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Monburan.ApiTests;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

static class NotificationChecks
{
    public static async Task RunAsync(string root)
    {
        var output = Path.Combine(Environment.GetEnvironmentVariable("TEMP")!, "opencode", "monburan-notifications");
        Directory.CreateDirectory(output);
        var control = new RepositoryControl();
        await using var factory = new NotificationsFactory(Path.Combine(root, "src", "Monburan.Web"), control);
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
            var candidate = await db.Candidates.FirstAsync(c => c.Email == "joao.silva@email.com");
            db.Notifications.AddRange(
                new Notification { RecipientId = candidate.Id.ToString(), Title = "Atualização da candidatura para Desenvolvedor .NET", Message = "Sua candidatura está em análise. Acompanhe as próximas etapas em Minhas candidaturas." },
                new Notification { RecipientId = candidate.Id.ToString(), Title = "Uma nova oportunidade corresponde ao seu alerta", Message = "Referência: " + new string('A', 180) },
                new Notification { RecipientId = candidate.Id.ToString(), Title = "Perfil atualizado", Message = "Suas informações foram salvas.", IsRead = true, ReadAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { Locale = "pt-BR" });
        await context.RouteAsync("**/*", async route =>
        {
            var url = route.Request.Url;
            if (url.StartsWith("data:") || url.StartsWith("blob:") || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsLoopback))
                await route.ContinueAsync();
            else
                await route.AbortAsync();
        });
        var page = await context.NewPageAsync();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(baseUrl + "/Account/Login");
        await page.GetByTestId("login-email").FillAsync("joao.silva@email.com");
        await page.GetByTestId("login-password").FillAsync("Monburan123");
        await page.GetByTestId("login-submit").ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login"));
        var metricsJs = await File.ReadAllTextAsync(Path.Combine(root, "tests", "ux-smoke", "Capture", "metrics.js"));
        var results = new List<object>();
        foreach (var theme in new[] { "light", "dark" })
        {
            await page.EvaluateAsync("theme => localStorage.setItem('monburan-theme', theme)", theme);
            foreach (var width in new[] { 320, 390, 834, 1440 })
            {
                await page.SetViewportSizeAsync(width, 900);
                await page.GotoAsync(baseUrl + "/notifications", new() { WaitUntil = WaitUntilState.NetworkIdle });
                await Assertions.Expect(page.GetByTestId("notification-item")).ToHaveCountAsync(3);
                await page.ScreenshotAsync(new() { Path = Path.Combine(output, $"notifications-{width}-{theme}.png"), FullPage = true });
                var metrics = await page.EvaluateAsync<JsonElement>(metricsJs);
                results.Add(new { width, theme, metrics });
                if (metrics.GetProperty("horizontalOverflow").GetInt32() > 1 || metrics.GetProperty("lowContrastCount").GetInt32() > 0
                    || metrics.GetProperty("smallTextCount").GetInt32() > 0 || metrics.GetProperty("smallTargetCount").GetInt32() > 0
                    || metrics.GetProperty("unlabeled").GetArrayLength() > 0 || metrics.GetProperty("nameless").GetArrayLength() > 0)
                    throw new InvalidOperationException($"Métricas inválidas em {width}/{theme}: {metrics}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

        var reload = page.GetByRole(AriaRole.Button, new() { Name = "Recarregar", Exact = true });
        control.Delay = true;
        control.Fail = true;
        await reload.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notifications-results")).ToHaveAttributeAsync("aria-busy", "true");
        await Assertions.Expect(page.GetByTestId("notification-item")).ToHaveCountAsync(3);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Marcar todas como lidas", Exact = true })).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByTestId("notifications-refresh-error")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("notification-item")).ToHaveCountAsync(3);
        control.Fail = false;
        await reload.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notifications-results")).ToHaveAttributeAsync("aria-busy", "false");
        await Assertions.Expect(page.GetByTestId("notifications-refresh-error")).ToHaveCountAsync(0);

        var mark = page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lida:", Exact = false }).First;
        await mark.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(reload).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lida:", Exact = false })).ToHaveCountAsync(1);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Atualizações recentes" })).ToBeFocusedAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Marcar todas como lidas", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lida:", Exact = false })).ToHaveCountAsync(0);
        await reload.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notifications-results")).ToHaveAttributeAsync("aria-busy", "false");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Marcar como lida:", Exact = false })).ToHaveCountAsync(0);
        Console.WriteLine($"NOTIFICATIONS_OK: 8 capturas, métricas, teclado, recarga com falha/recuperação e marcação persistida. ARTIFACTS={output}");
    }

    sealed class RepositoryControl
    {
        public bool Delay { get; set; }
        public bool Fail { get; set; }
    }

    sealed class NotificationsFactory(string contentRoot, RepositoryControl control) : MonburanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseContentRoot(contentRoot);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INotificationRepository>();
                services.AddScoped<INotificationRepository>(provider => new ControlledRepository(
                    provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), control));
                services.RemoveAll<IBaseRepository<Notification>>();
                services.AddScoped<IBaseRepository<Notification>>(provider => provider.GetRequiredService<INotificationRepository>());
            });
        }
    }

    sealed class ControlledRepository(IDbContextFactory<ApplicationDbContext> db, RepositoryControl control)
        : NotificationRepository(db), INotificationRepository
    {
        async Task<RequestResult<IEnumerable<Notification>>> INotificationRepository.ListByRecipientAsync(string recipientId, int take, CancellationToken cancellationToken)
        {
            if (control.Delay) await Task.Delay(1200, cancellationToken);
            if (control.Fail) throw new InvalidOperationException("Falha de recarga simulada");
            return await base.ListByRecipientAsync(recipientId, take, cancellationToken);
        }

        async Task<RequestResult<Notification>> IBaseRepository<Notification>.GetByIdAsync(Id id, CancellationToken cancellationToken)
        {
            if (control.Delay) await Task.Delay(1200, cancellationToken);
            return await base.GetByIdAsync(id, cancellationToken);
        }
    }
}
