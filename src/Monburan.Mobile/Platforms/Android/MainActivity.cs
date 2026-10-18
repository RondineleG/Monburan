using Android.App;
using Android.Content.PM;
using Android.OS;
using Monburan.Mobile.Services;

namespace Monburan.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnResume()
    {
        base.OnResume();
        // O tema guardado vale já na partida a frio; a troca em tempo de execução é tratada em NativeTheme.Apply
        NativeTheme.ApplySystemBars(this, NativeTheme.Saved == AppTheme.Dark);
    }
}
