using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Services;

using Xunit;

namespace Monburan.IntegrationTests.Services;

/// <summary>
/// Vínculo conta ↔ perfil (feito pelo e-mail) na confirmação, troca de e-mail e exclusão de conta.
/// </summary>
public class AccountProfileServiceTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly AccountProfileService _service;

    public AccountProfileServiceTests()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();
        _factory = _provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        _service = new AccountProfileService(_factory, NullLogger<AccountProfileService>.Instance);
    }

    [Fact]
    public async Task MarkEmailVerified_UpdatesProfileIgnoringCase()
    {
        var candidate = await AddCandidateAsync("Pessoa@Email.com");

        await _service.MarkEmailVerifiedAsync("pessoa@email.com");

        (await FindCandidateAsync(candidate.Id)).IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmail_KeepsProfileLinkedToTheAccount()
    {
        var candidate = await AddCandidateAsync("antigo@email.com");

        await _service.ChangeEmailAsync("antigo@email.com", "novo@email.com");

        var updated = await FindCandidateAsync(candidate.Id);
        updated.Email.Should().Be("novo@email.com");
        updated.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailUsedByAnotherProfile_DetectsConflicts()
    {
        await AddCandidateAsync("ocupado@email.com");
        await AddCandidateAsync("meu@email.com");

        (await _service.IsEmailUsedByAnotherProfileAsync("ocupado@email.com", "meu@email.com")).Should().BeTrue();
        (await _service.IsEmailUsedByAnotherProfileAsync("livre@email.com", "meu@email.com")).Should().BeFalse();
        (await _service.IsEmailUsedByAnotherProfileAsync("meu@email.com", "meu@email.com")).Should().BeFalse();
    }

    [Fact]
    public async Task AnonymizeProfiles_RemovesPersonalDataAndFreesTheEmail()
    {
        var candidate = await AddCandidateAsync("lgpd@email.com", phone: "(11) 91111-2222");
        await using (var context = await _factory.CreateDbContextAsync())
        {
            context.Resumes.Add(new Resume
            {
                Id = Id.Create(Guid.NewGuid()),
                CandidateId = candidate.Id,
                Title = "Meu currículo",
                Summary = "Contato: (11) 91111-2222",
                TenantId = "default"
            });
            await context.SaveChangesAsync();
        }

        await _service.AnonymizeProfilesAsync("lgpd@email.com");

        var anonymized = await FindCandidateAsync(candidate.Id);
        anonymized.Name.Should().Be("Conta excluída");
        anonymized.Email.Should().EndWith("@monburan.invalid");
        anonymized.Phone.Should().BeNull();
        anonymized.IsDeleted.Should().BeTrue();

        await using var check = await _factory.CreateDbContextAsync();
        var resume = await check.Resumes.SingleAsync(r => r.CandidateId == candidate.Id);
        resume.IsDeleted.Should().BeTrue();
        resume.Summary.Should().BeNull();
    }

    private async Task<Candidate> AddCandidateAsync(string email, string? phone = null)
    {
        var candidate = new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Pessoa de Teste",
            Email = email,
            Phone = phone,
            Status = EAccountStatus.Active,
            TenantId = "default"
        };

        await using var context = await _factory.CreateDbContextAsync();
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();
        return candidate;
    }

    private async Task<Candidate> FindCandidateAsync(Id id)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Candidates.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    public void Dispose() => _provider.Dispose();
}
