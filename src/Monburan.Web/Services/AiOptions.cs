namespace Monburan.Web.Services;

/// <summary>
/// Configuração da IA (seção <c>Ai</c>): qualquer serviço compatível com o protocolo de "chat completions" da
/// OpenAI serve. Sem chave, URL e modelo, nenhum recurso de IA é oferecido.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Raiz da API, com o caminho de versão (ex.: <c>https://servidor/v1</c>). O cliente acrescenta
    /// <c>/chat/completions</c>. Exige https, exceto em endereço local (testes e servidores locais).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Chave de acesso. Nunca vai para o repositório: em desenvolvimento fica nos segredos de usuário
    /// (<c>dotnet user-secrets</c>) e em produção na variável de ambiente <c>Ai__ApiKey</c>.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    /// <summary>Nome mostrado à pessoa como destino do conteúdo (ex.: "Xiaomi MiMo"); vazio usa o endereço do servidor.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Tempo máximo de uma chamada. Modelos de raciocínio levam dezenas de segundos.</summary>
    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>Limite de tokens da resposta. Em modelos de raciocínio o raciocínio também conta, então precisa de folga.</summary>
    public int MaxOutputTokens { get; set; } = 4000;

    public double Temperature { get; set; } = 0.4;

    /// <summary>Pede <c>response_format: json_object</c>. Desligue para servidores que não aceitam o campo.</summary>
    public bool UseJsonResponseFormat { get; set; } = true;

    /// <summary>Quantas gerações cada pessoa pode pedir por hora (cada chamada tem custo).</summary>
    public int MaxRequestsPerHour { get; set; } = 5;

    /// <summary>Endereço de <c>chat/completions</c>, ou <c>null</c> quando <see cref="BaseUrl"/> não é válida ou segura.</summary>
    public Uri? ChatCompletionsUri
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BaseUrl)
                || !Uri.TryCreate(BaseUrl.Trim().TrimEnd('/') + "/chat/completions", UriKind.Absolute, out var uri))
                return null;

            // A chave viaja no cabeçalho: nunca em texto puro pela internet
            return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) ? uri : null;
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model) && ChatCompletionsUri is not null;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(ProviderName) ? ProviderName.Trim() : ChatCompletionsUri?.Host ?? "provedor de IA";
}
