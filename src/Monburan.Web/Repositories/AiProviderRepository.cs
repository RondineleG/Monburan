using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class AiProviderRepository : IAiProviderRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public AiProviderRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<AiProvider>> CreateAsync(AiProvider entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.AiProviders.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<AiProvider>.Success(entity);
    }

    public async Task<RequestResult<AiProvider>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var provider = await context.AiProviders.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (provider is null)
            return RequestResult<AiProvider>.EntityNotFound("AiProvider", id.Value, "Provedor não encontrado.");

        // Exclusão lógica; some da lista e libera o nome para um provedor novo
        provider.IsDeleted = true;
        provider.DeletedAt = DateTime.UtcNow;
        provider.IsDefault = false;
        provider.IsEnabled = false;
        await context.SaveChangesAsync();
        return RequestResult<AiProvider>.Success(provider);
    }

    public async Task<RequestResult<IEnumerable<AiProvider>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var providers = await context.AiProviders
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<AiProvider>>.Success(providers);
    }

    public async Task<RequestResult<AiProvider>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var provider = await context.AiProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        return provider is null
            ? RequestResult<AiProvider>.EntityNotFound("AiProvider", id.Value, "Provedor não encontrado.")
            : RequestResult<AiProvider>.Success(provider);
    }

    public async Task<RequestResult<AiProvider>> UpdateAsync(AiProvider entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var provider = await context.AiProviders.FirstOrDefaultAsync(p => p.Id == entity.Id && !p.IsDeleted);
        if (provider is null)
            return RequestResult<AiProvider>.EntityNotFound("AiProvider", entity.Id.Value, "Provedor não encontrado.");

        // Só os campos editáveis; o padrão muda apenas por SetDefaultAsync
        provider.Name = entity.Name;
        provider.Protocol = entity.Protocol;
        provider.BaseUrl = entity.BaseUrl;
        provider.Model = entity.Model;
        provider.ApiKeyEnvVar = entity.ApiKeyEnvVar;
        provider.IsEnabled = entity.IsEnabled;
        provider.TimeoutSeconds = entity.TimeoutSeconds;
        provider.MaxOutputTokens = entity.MaxOutputTokens;
        provider.Temperature = entity.Temperature;
        provider.SendTemperature = entity.SendTemperature;
        provider.UseJsonResponseFormat = entity.UseJsonResponseFormat;
        provider.UseMaxCompletionTokens = entity.UseMaxCompletionTokens;
        provider.MarkUpdated(entity.UpdatedBy);

        await context.SaveChangesAsync();
        return RequestResult<AiProvider>.Success(provider);
    }

    public async Task<RequestResult<IEnumerable<AiProvider>>> ListAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var providers = await context.AiProviders
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return RequestResult<IEnumerable<AiProvider>>.Success(providers);
    }

    public async Task<bool> NameExistsAsync(string name, Id? exceptId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var wanted = name.Trim().ToLower();
        return await context.AiProviders
            .AsNoTracking()
            .AnyAsync(p => !p.IsDeleted && p.Name.ToLower() == wanted && (exceptId == null || p.Id != exceptId));
    }

    public async Task<int> CountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AiProviders.CountAsync(p => !p.IsDeleted);
    }

    public async Task<RequestResult<AiProvider>> SetDefaultAsync(Id id, string updatedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var providers = await context.AiProviders.Where(p => !p.IsDeleted).ToListAsync();
        var target = providers.FirstOrDefault(p => p.Id == id);
        if (target is null)
            return RequestResult<AiProvider>.EntityNotFound("AiProvider", id.Value, "Provedor não encontrado.");

        // Numa transação: ou troca o padrão por completo ou nada muda (nunca fica sem padrão por uma falha no meio)
        await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync() : null;

        // Primeiro tira a marca do padrão atual e confirma, para o índice de "um só padrão" nunca ver dois
        var previous = providers.Where(p => p.IsDefault && p.Id != id).ToList();
        if (previous.Count > 0)
        {
            foreach (var provider in previous)
            {
                provider.IsDefault = false;
                provider.MarkUpdated(updatedBy);
            }

            await context.SaveChangesAsync();
        }

        if (!target.IsDefault)
        {
            target.IsDefault = true;
            target.MarkUpdated(updatedBy);
            await context.SaveChangesAsync();
        }

        if (transaction is not null)
            await transaction.CommitAsync();

        return RequestResult<AiProvider>.Success(target);
    }
}
