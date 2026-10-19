using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;

namespace Monburan.Web.Repositories;

/// <summary>
/// Grava o perfil na mesma unidade de trabalho da conta do Identity. Diferente dos outros
/// repositórios, que abrem a própria conexão, aqui usa o <see cref="ApplicationDbContext"/> do
/// escopo — o mesmo que o <c>UserManager</c> usa. Assim, cadastro e perfil entram juntos na
/// transação aberta pelo cadastro.
/// </summary>
public class AccountProfileRepository : IAccountProfileRepository
{
    private readonly ApplicationDbContext _context;

    public AccountProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RequestResult<Candidate>> CreateCandidateAsync(Candidate candidate)
    {
        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync();
        return RequestResult<Candidate>.Success(candidate);
    }

    public async Task<RequestResult<Advertiser>> CreateAdvertiserAsync(Advertiser advertiser)
    {
        _context.Advertisers.Add(advertiser);
        await _context.SaveChangesAsync();
        return RequestResult<Advertiser>.Success(advertiser);
    }
}
