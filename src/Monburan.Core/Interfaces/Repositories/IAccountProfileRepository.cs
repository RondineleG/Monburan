using Monburan.Core.Entities;
using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Repositories;

/// <summary>
/// Grava o perfil de candidato/anunciante na mesma unidade de trabalho da conta do Identity.
/// Usado no cadastro: a conta e o perfil têm de ser gravados (ou desfeitos) juntos. Com o
/// perfil gravado em outra conexão, uma falha no fim do cadastro deixaria um perfil órfão
/// sem conta — e o e-mail ficaria travado pelo índice único.
/// </summary>
public interface IAccountProfileRepository
{
    Task<RequestResult<Candidate>> CreateCandidateAsync(Candidate candidate, CancellationToken cancellationToken = default);

    Task<RequestResult<Advertiser>> CreateAdvertiserAsync(Advertiser advertiser, CancellationToken cancellationToken = default);
}
