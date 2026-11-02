namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Pseudonimiza a chave do remetente (IP ou bloco /64). Precisa ser uma função com segredo: um hash simples de um IPv4
/// se reverte por força bruta (2³² possibilidades), o que anularia a pseudonimização.
/// </summary>
public interface ISubmitterKeyHasher
{
    string Hash(string value);
}
