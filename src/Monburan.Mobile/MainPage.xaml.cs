namespace Monburan.Mobile;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
#if ANDROID
		// O Android 15+ desenha o app por baixo das barras do sistema; sem isto o cabeçalho e a navegação inferior ficam
		// cobertos pela barra de status e pelo indicador de gestos. No iOS o WebView já recebe a área segura.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
#endif
	}
}
