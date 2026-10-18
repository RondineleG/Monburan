namespace Monburan.Mobile.Services;

/// <summary>
/// O tema do app (claro ou escuro) é escolhido pelo usuário dentro do WebView e não segue o tema do sistema.
/// Esta classe leva essa escolha para o MAUI, que a usa na cor da página atrás das barras do sistema e no estilo
/// dos ícones da barra de status. Sem isso, com o sistema claro e o app escuro, as faixas das barras sairiam claras
/// em volta de uma interface escura (e os ícones, escuros sobre o fundo escuro).
/// </summary>
public static class NativeTheme
{
    private const string PreferenceKey = "monburan-theme";

    /// <summary>Tema guardado; vale desde a partida a frio, antes de o WebView carregar. O padrão é escuro, como na web.</summary>
    public static AppTheme Saved =>
        Preferences.Default.Get(PreferenceKey, "dark") == "light" ? AppTheme.Light : AppTheme.Dark;

    public static void Apply(bool isDark)
    {
        Preferences.Default.Set(PreferenceKey, isDark ? "dark" : "light");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is { } app)
                app.UserAppTheme = isDark ? AppTheme.Dark : AppTheme.Light;
#if ANDROID
            ApplySystemBars(Platform.CurrentActivity, isDark);
#endif
        });
    }

#if ANDROID
    /// <summary>
    /// No Android 15+ os ícones da barra de status ficam sempre claros, independentemente do tema do app ou do sistema:
    /// sobre o fundo claro do tema claro eles somem. Aqui eles ficam escuros sobre fundo claro e claros sobre fundo escuro.
    /// </summary>
    public static void ApplySystemBars(Android.App.Activity? activity, bool isDark)
    {
        if (activity?.Window is not { } window)
            return;

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is null)
            return;

        controller.AppearanceLightStatusBars = !isDark;
        controller.AppearanceLightNavigationBars = !isDark;
    }
#endif
}
