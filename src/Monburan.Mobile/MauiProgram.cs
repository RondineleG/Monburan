using Microsoft.Extensions.Logging;

using Monburan.Mobile.Services;
using Monburan.Mobile.Shared;
using Monburan.Mobile.Shared.Auth;

namespace Monburan.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
		builder.Services.AddMonburanApi(MobileApiConfig.BaseUrl);

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
