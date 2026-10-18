namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Lê as chaves de IA do ambiente do servidor (variáveis <c>AI_KEY_*</c>; em desenvolvimento, segredos de usuário).
/// Só nomes no formato permitido são lidos: o banco guarda o nome da variável, nunca a chave, e uma variável fora do
/// padrão (senha do banco, chave do JWT...) nunca é acessível por aqui.
/// </summary>
public interface IAiSecretReader
{
    /// <summary>A variável existe e tem valor.</summary>
    bool IsSet(string envVarName);

    /// <summary>Valor da variável, ou <c>null</c> quando o nome é inválido, não existe ou está vazio.</summary>
    string? Get(string envVarName);
}
