using System.Security.Cryptography;
using System.Text;
using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// HMAC-SHA256 com a chave secreta <c>Submissions:HashKey</c> (variável <c>Submissions__HashKey</c>, no Compose
/// <c>SUBMITTER_HASH_KEY</c>). Custa o mesmo que o SHA-256 puro, mas sem a chave ninguém reverte o IP a partir do valor
/// gravado. Sem chave configurada, gera uma aleatória para o processo: continua seguro, e só deixa de correlacionar o
/// mesmo remetente entre reinícios (o limite em memória já zera no reinício).
/// </summary>
public sealed class HmacSubmitterKeyHasher : ISubmitterKeyHasher
{
    private readonly byte[] _key;

    public HmacSubmitterKeyHasher(IConfiguration configuration, ILogger<HmacSubmitterKeyHasher> logger)
    {
        var configured = configuration["Submissions:HashKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            _key = RandomNumberGenerator.GetBytes(32);
            logger.LogWarning("Submissions:HashKey não configurada: usando uma chave aleatória deste processo. Defina SUBMITTER_HASH_KEY para manter o hash estável entre reinícios.");
        }
        else
        {
            _key = Encoding.UTF8.GetBytes(configured);
        }
    }

    public string Hash(string value) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
