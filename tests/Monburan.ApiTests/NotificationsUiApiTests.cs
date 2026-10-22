using System.Net;

using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

namespace Monburan.ApiTests;

public class NotificationsUiApiTests
{
    [Fact]
    public async Task Notifications_ShowOwnMessagesWithContextualActionsAndDates()
    {
        await using var factory = new MonburanApiFactory();
        using var client = CreateClient(factory);
        await CookieAuth.LoginAsync(client, "joao.silva@email.com", "Monburan123");
        using var scope = factory.Services.CreateScope();
        var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await dbf.CreateDbContextAsync();
        var candidate = await db.Candidates.FirstAsync(c => c.Email == "joao.silva@email.com");
        db.Notifications.AddRange(
            new Notification { RecipientId = candidate.Id.ToString(), Title = "Convite para entrevista", Message = "Confira as atualizações da sua candidatura." },
            new Notification { RecipientId = "outra-conta", Title = "Mensagem privada de outra conta", Message = "Conteúdo privado" });
        await db.SaveChangesAsync();

        var response = await client.GetAsync("/notifications");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Convite para entrevista")
            .And.Contain("aria-label=\"Marcar como lida: Convite para entrevista\"")
            .And.Contain("<time datetime=")
            .And.Contain("data-testid=\"notifications-results\"")
            .And.Contain("Resumo das notificações recentes");
        html.Should().NotContain("Mensagem privada de outra conta");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialLoadFailure_ShowsRecoveryWithoutFalseEmptyStateOrCounts(bool throws)
    {
        await using var factory = new FailingNotificationsFactory(throws);
        using var client = CreateClient(factory);
        await CookieAuth.LoginAsync(client, "joao.silva@email.com", "Monburan123");

        var response = await client.GetAsync("/notifications");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Não foi possível carregar as notificações")
            .And.Contain("data-testid=\"error-retry\"");
        html.Should().NotContain("data-testid=\"notifications-empty\"")
            .And.NotContain("notification-metric__value");
    }

    private static HttpClient CreateClient(MonburanApiFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private sealed class FailingNotificationsFactory(bool throws) : MonburanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INotificationRepository>();
                services.AddScoped<INotificationRepository>(provider => new FailingNotificationsRepository(
                    provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), throws));
            });
        }
    }

    private sealed class FailingNotificationsRepository(IDbContextFactory<ApplicationDbContext> db, bool throws)
        : NotificationRepository(db), INotificationRepository
    {
        Task<RequestResult<IEnumerable<Notification>>> INotificationRepository.ListByRecipientAsync(
            string recipientId, int take, CancellationToken cancellationToken) => throws
                ? throw new InvalidOperationException("Falha simulada")
                : Task.FromResult(RequestResult<IEnumerable<Notification>>.WithError("Falha simulada"));
    }
}
