namespace Monburan.Mobile.Preview;

public static class PreviewPaths
{
    // Raiz do repositório descoberta subindo diretórios até achar Monburan.sln
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string MobileWwwroot => Path.Combine(RepoRoot, "src", "Monburan.Mobile", "wwwroot");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Monburan.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Monburan.sln não encontrado");
    }
}
