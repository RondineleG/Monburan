using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Web.Data;
using Monburan.Web.Repositories;

using Xunit;

namespace Monburan.IntegrationTests.Repositories;

public class AdminAuditLogTests : IDisposable
{
    private static readonly AdminActor Admin = new(Guid.NewGuid().ToString(), "admin@monburan.com.br");

    private readonly ServiceProvider _serviceProvider;
    private readonly AdminAuditLog _log;

    public AdminAuditLogTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        _serviceProvider = services.BuildServiceProvider();
        _log = new AdminAuditLog(
            _serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), NullLogger<AdminAuditLog>.Instance);
    }

    public void Dispose() => _serviceProvider.Dispose();

    private Task RecordAsync(string action, string targetType, string label, string? details = null, AdminActor? actor = null) =>
        _log.RecordAsync(new AdminAuditRecord(action, actor ?? Admin, targetType, Guid.NewGuid().ToString(), label, details));

    [Fact]
    public async Task Record_KeepsWhoWhenWhatAndOnWhom()
    {
        await RecordAsync(AdminAuditActions.AccountDeactivated, AdminAuditActions.AccountTarget, "joao@exemplo.com", "abuso de anúncios");

        var page = await _log.ListAsync(new AdminAuditFilter(), 1, 25);

        var entry = page.Data.Should().ContainSingle().Which;
        entry.Action.Should().Be("account.deactivated");
        entry.ActorId.Should().Be(Admin.Id);
        entry.ActorEmail.Should().Be("admin@monburan.com.br");
        entry.TargetType.Should().Be("account");
        entry.TargetLabel.Should().Be("joao@exemplo.com");
        entry.Details.Should().Be("abuso de anúncios");
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        entry.CreatedBy.Should().Be("admin@monburan.com.br");
    }

    [Fact]
    public async Task List_ShowsTheNewestFirst_AndPages()
    {
        for (var index = 1; index <= 7; index++)
        {
            await RecordAsync(AdminAuditActions.JobStatusChanged, AdminAuditActions.JobTarget, $"Vaga {index}");
            await Task.Delay(5);
        }

        var first = await _log.ListAsync(new AdminAuditFilter(), 1, 3);
        var last = await _log.ListAsync(new AdminAuditFilter(), 3, 3);

        first.TotalCount.Should().Be(7);
        first.TotalPages.Should().Be(3);
        first.Data.Select(entry => entry.TargetLabel).Should().Equal("Vaga 7", "Vaga 6", "Vaga 5");
        last.Data.Select(entry => entry.TargetLabel).Should().Equal("Vaga 1");
    }

    [Fact]
    public async Task List_FiltersByActionTargetAndSearchTerm()
    {
        await RecordAsync(AdminAuditActions.AccountDeactivated, AdminAuditActions.AccountTarget, "maria@exemplo.com", "spam");
        await RecordAsync(AdminAuditActions.JobDeleted, AdminAuditActions.JobTarget, "Vaga de Estágio", "conteúdo enganoso");
        await RecordAsync(AdminAuditActions.AiProviderSaved, AdminAuditActions.AiProviderTarget, "OpenRouter");

        (await _log.ListAsync(new AdminAuditFilter(Action: AdminAuditActions.JobDeleted), 1, 25)).Data
            .Should().ContainSingle().Which.TargetLabel.Should().Be("Vaga de Estágio");
        (await _log.ListAsync(new AdminAuditFilter(TargetType: AdminAuditActions.AiProviderTarget), 1, 25)).Data
            .Should().ContainSingle().Which.TargetLabel.Should().Be("OpenRouter");
        (await _log.ListAsync(new AdminAuditFilter(SearchTerm: "MARIA"), 1, 25)).Data
            .Should().ContainSingle().Which.Action.Should().Be("account.deactivated");
        (await _log.ListAsync(new AdminAuditFilter(SearchTerm: "enganoso"), 1, 25)).Data
            .Should().ContainSingle().Which.TargetLabel.Should().Be("Vaga de Estágio");
        (await _log.ListAsync(new AdminAuditFilter(SearchTerm: "admin@monburan"), 1, 25)).TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Record_TruncatesWhatDoesNotFitTheColumns_InsteadOfLosingTheEntry()
    {
        var longEmail = new string('a', 240) + "@exemplo.com";

        await RecordAsync(
            AdminAuditActions.JobStatusChanged, AdminAuditActions.JobTarget, new string('t', 500), new string('d', 3000),
            new AdminActor(Admin.Id, longEmail));

        var entry = (await _log.ListAsync(new AdminAuditFilter(), 1, 25)).Data.Single();
        entry.TargetLabel.Length.Should().Be(200);
        entry.TargetLabel.Should().EndWith("…");
        entry.Details!.Length.Should().Be(1000);
        entry.CreatedBy.Length.Should().Be(100);
        entry.ActorEmail.Should().Be(longEmail);
    }

    [Fact]
    public async Task Record_NeverThrows_WhenTheDatabaseFails()
    {
        var failing = new AdminAuditLog(new ThrowingContextFactory(), NullLogger<AdminAuditLog>.Instance);

        var act = () => failing.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.AccountDeactivated, Admin, AdminAuditActions.AccountTarget, "1", "alguem@exemplo.com"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApplicationLog_CarriesIdentifiersAndTheActionName_NeverEmailsOrNames()
    {
        var logger = new CapturingLogger();
        var actor = new AdminActor(Guid.NewGuid().ToString(), "quem.agiu@exemplo.com");
        var working = new AdminAuditLog(_serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(), logger);
        var failing = new AdminAuditLog(new ThrowingContextFactory(), logger);

        await working.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.AccountEmailConfirmed, actor, AdminAuditActions.AccountTarget, "42", "alvo.da.acao@exemplo.com"));
        await failing.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.AccountDeactivated, actor, AdminAuditActions.AccountTarget, "43", "outro.alvo@exemplo.com"));

        // a ação que grava (1 linha) e a que falha ao gravar (a linha da ação e a do erro)
        logger.Messages.Should().HaveCount(3);
        logger.Messages.Should().OnlyContain(message => message.Contains(actor.Id));
        logger.Messages.Should().OnlyContain(message =>
            !message.Contains("exemplo.com") && !message.Contains("quem.agiu") && !message.Contains("alvo"));
        logger.Messages[0].Should().Contain("E-mail confirmado manualmente").And.Contain("account 42");
    }

    [Fact]
    public async Task List_ClampsStrangePageNumbers()
    {
        await RecordAsync(AdminAuditActions.AccountActivated, AdminAuditActions.AccountTarget, "ana@exemplo.com");

        var page = await _log.ListAsync(new AdminAuditFilter(), -5, 0);

        page.Page.Should().Be(1);
        page.Size.Should().Be(25);
        page.Data.Should().ContainSingle();
    }

    private sealed class CapturingLogger : ILogger<AdminAuditLog>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw new InvalidOperationException("banco fora do ar");

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("banco fora do ar");
    }
}
