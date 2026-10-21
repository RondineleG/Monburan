// Substitutos das APIs de plataforma usadas pelos componentes do app. Só existem na pré-visualização: o app de
// verdade usa as do MAUI. Nada aqui imita comportamento nativo além do necessário para a tela renderizar.

namespace Monburan.Mobile
{
    /// <summary>Âncora do assembly para o Router do app (Routes.razor usa typeof(MauiProgram).Assembly).</summary>
    public static class MauiProgram;
}

namespace Monburan.Mobile.Services
{
    /// <summary>No app, leva o tema às barras do sistema; no navegador não há barras para pintar.</summary>
    public static class NativeTheme
    {
        public static void Apply(bool isDark)
        {
        }
    }
}

namespace Microsoft.Maui.Storage
{
    public interface IFilePicker
    {
        Task<FileResult?> PickAsync(PickOptions? options = null);
    }

    /// <summary>Seletor de arquivo sempre "cancelado": o envio de PDF só é testável no aparelho.</summary>
    public static class FilePicker
    {
        public static IFilePicker Default { get; } = new CancelledPicker();

        private sealed class CancelledPicker : IFilePicker
        {
            public Task<FileResult?> PickAsync(PickOptions? options = null) => Task.FromResult<FileResult?>(null);
        }
    }

    public sealed class PickOptions
    {
        public string? PickerTitle { get; set; }
        public FilePickerFileType? FileTypes { get; set; }
    }

    public sealed class FilePickerFileType
    {
        public static FilePickerFileType Pdf { get; } = new();
    }

    public sealed class FileResult
    {
        public string FileName { get; init; } = string.Empty;
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(Stream.Null);
    }
}
