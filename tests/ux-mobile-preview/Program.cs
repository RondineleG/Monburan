using Microsoft.Extensions.FileProviders;
using Monburan.Mobile.Preview;
using Monburan.Mobile.Shared;
using Monburan.Mobile.Shared.Auth;

// Uso: ux-mobile-preview --api http://127.0.0.1:5095/ [--urls http://127.0.0.1:5096]
// A API é a do site (ex.: o host InMemory do ux-smoke); o app conversa com ela como no aparelho.
var apiIndex = Array.IndexOf(args, "--api");
if (apiIndex < 0 || apiIndex + 1 >= args.Length || !Uri.TryCreate(args[apiIndex + 1], UriKind.Absolute, out var apiBase))
{
    Console.Error.WriteLine("Informe a API: --api http://127.0.0.1:5095/");
    return 1;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<ITokenStore, MemoryTokenStore>();
builder.Services.AddMonburanApi(apiBase);

var app = builder.Build();
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(PreviewPaths.MobileWwwroot) });
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<PreviewHost>().AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine($"PREVIEW={string.Join(",", app.Urls)}"));
await app.RunAsync();
return 0;
