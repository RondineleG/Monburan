namespace Monburan.Web.Services;

/// <summary>
/// Ajustes gerais da IA (seção <c>Ai</c>). Os provedores em si (endereço, modelo, protocolo e a variável da chave) não
/// ficam aqui: o administrador cadastra pela tela "Provedores de IA" e eles vivem no banco.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Quantas gerações cada pessoa pode pedir por hora (cada chamada tem custo).</summary>
    public int MaxRequestsPerHour { get; set; } = 5;

    /// <summary>
    /// Permite que o endereço de um provedor aponte para a rede interna ou para o próprio computador (um Ollama local,
    /// por exemplo). Só em desenvolvimento: em produção fica falso, senão um provedor cadastrado poderia fazer o
    /// servidor acessar serviços internos (banco, painel, metadados da nuvem). Não é editável pela tela.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>De quantos em quantos segundos cada instância relê os provedores do banco (alterações feitas em outra instância).</summary>
    public int CatalogRefreshSeconds { get; set; } = 60;
}
