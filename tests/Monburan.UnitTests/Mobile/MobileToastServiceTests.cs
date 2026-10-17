using FluentAssertions;

using Monburan.Mobile.Shared.Ui;

namespace Monburan.UnitTests.Mobile;

public class MobileToastServiceTests
{
    [Fact]
    public void Show_StacksAndDismisses()
    {
        using var toasts = new ToastService();

        toasts.ShowSuccess("Candidatura enviada");
        toasts.ShowError("Sem conexão");

        toasts.Toasts.Select(t => t.Level).Should().Equal(ToastLevel.Success, ToastLevel.Error);

        toasts.Dismiss(toasts.Toasts[0].Id);
        toasts.Toasts.Should().ContainSingle(t => t.Message == "Sem conexão");
    }
}
