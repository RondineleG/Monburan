using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class ToastServiceTests
{
    [Fact]
    public void Show_AddsToastAndRaisesChanged()
    {
        using var toasts = new ToastService();
        var changes = 0;
        toasts.Changed += () => changes++;

        toasts.ShowSuccess("Vaga publicada", "Pronto");

        toasts.Toasts.Should().ContainSingle(t => t.Level == ToastLevel.Success && t.Message == "Vaga publicada" && t.Title == "Pronto");
        changes.Should().Be(1);
    }

    [Fact]
    public void Show_KeepsOnlyTheMostRecentToasts()
    {
        using var toasts = new ToastService();

        for (var i = 1; i <= ToastService.MaxVisible + 2; i++)
            toasts.ShowInfo($"Aviso {i}");

        toasts.Toasts.Should().HaveCount(ToastService.MaxVisible);
        toasts.Toasts.First().Message.Should().Be("Aviso 3");
        toasts.Toasts.Last().Message.Should().Be($"Aviso {ToastService.MaxVisible + 2}");
    }

    [Fact]
    public void Show_IgnoresBlankMessages()
    {
        using var toasts = new ToastService();

        toasts.ShowError("   ");

        toasts.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void Dismiss_RemovesToast()
    {
        using var toasts = new ToastService();
        toasts.ShowWarning("Atenção");
        var id = toasts.Toasts.Single().Id;

        toasts.Dismiss(id);

        toasts.Toasts.Should().BeEmpty();
    }

    [Fact]
    public async Task Toasts_AreDismissedAutomatically()
    {
        using var toasts = new ToastService
        {
            DefaultDuration = TimeSpan.FromMilliseconds(30),
            ErrorDuration = TimeSpan.FromMilliseconds(30)
        };
        toasts.ShowSuccess("Salvo");
        toasts.ShowError("Falhou");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (toasts.Toasts.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        toasts.Toasts.Should().BeEmpty();
    }
}
