using Monburan.Core.Interfaces.Services;
using Monburan.Core.Validations;

namespace Monburan.Web.Services;

/// <summary>
/// Lê as chaves de IA da configuração do servidor: variáveis de ambiente em produção e segredos de usuário em
/// desenvolvimento. Só nomes <c>AI_KEY_*</c> são lidos (ver <see cref="AiProviderRules.EnvVarPrefix"/>), então um
/// provedor cadastrado não consegue ler a senha do banco, a chave do JWT nem nenhum outro segredo da aplicação.
/// </summary>
public sealed class AiSecretReader : IAiSecretReader
{
    private readonly IConfiguration _configuration;

    public AiSecretReader(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsSet(string envVarName) => Get(envVarName) is not null;

    public string? Get(string envVarName)
    {
        if (!AiProviderRules.IsValidEnvVarName(envVarName))
            return null;

        var value = _configuration[envVarName]?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
