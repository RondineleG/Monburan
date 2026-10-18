using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Ai;

/// <summary>
/// Cria ou edita um provedor de IA. Regras: nome único; endereço seguro (https); a variável da chave só pode ser
/// <c>AI_KEY_*</c>; o padrão precisa estar ativo e não pode ser desativado sem antes escolher outro; o primeiro
/// provedor ativo vira o padrão sozinho.
/// </summary>
public class SaveAiProviderUseCase : IUseCase<AiProviderRequest, AiProviderResponse>
{
    private readonly IAiProviderRepository _repository;
    private readonly IAiProviderCatalog _catalog;
    private readonly IAiSecretReader _secrets;

    public SaveAiProviderUseCase(IAiProviderRepository repository, IAiProviderCatalog catalog, IAiSecretReader secrets)
    {
        _repository = repository;
        _catalog = catalog;
        _secrets = secrets;
    }

    public async Task<RequestResult<AiProviderResponse>> ExecuteAsync(AiProviderRequest request)
    {
        var errors = AiProviderValidator.Validate(request);

        AiProvider? current = null;
        if (request.Id is { } providerId)
        {
            var found = await _repository.GetByIdAsync(Id.Create(providerId));
            if (found.Status != EResultStatus.Success || found.Data is null)
                return RequestResult<AiProviderResponse>.EntityNotFound("AiProvider", providerId, "Provedor não encontrado.");

            current = found.Data;
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (errors.All(error => error.Field != nameof(request.Name)) && await _repository.NameExistsAsync(name, current?.Id))
            errors.Add((nameof(request.Name), "Já existe um provedor com esse nome."));

        if (current is { IsDefault: true } && !request.IsEnabled)
            errors.Add((nameof(request.IsEnabled), "Escolha outro provedor como padrão antes de desativar este."));

        if (request.MakeDefault && !request.IsEnabled)
            errors.Add((nameof(request.MakeDefault), "O provedor padrão precisa estar ativo."));

        if (errors.Count > 0)
            return ValidationFailure(errors);

        var provider = current ?? new AiProvider
        {
            Id = Id.Create(Guid.NewGuid()),
            TenantId = "default",
            CreatedBy = request.UpdatedBy
        };

        provider.Name = name;
        provider.Protocol = request.Protocol;
        provider.BaseUrl = AiProviderRules.NormalizeBaseUrl(request.BaseUrl);
        provider.Model = request.Model.Trim();
        provider.ApiKeyEnvVar = request.ApiKeyEnvVar.Trim();
        provider.IsEnabled = request.IsEnabled;
        provider.TimeoutSeconds = request.TimeoutSeconds;
        provider.MaxOutputTokens = request.MaxOutputTokens;
        provider.Temperature = request.Temperature;
        provider.SendTemperature = request.SendTemperature;
        provider.UseJsonResponseFormat = request.UseJsonResponseFormat;
        provider.UseMaxCompletionTokens = request.UseMaxCompletionTokens;
        provider.UpdatedBy = request.UpdatedBy;

        var makeDefault = request.MakeDefault;
        RequestResult<AiProvider> saved;
        if (current is null)
        {
            // O primeiro provedor ativo vira o padrão sozinho
            if (request.IsEnabled && await _repository.CountAsync() == 0)
                makeDefault = true;

            saved = await _repository.CreateAsync(provider);
        }
        else
        {
            saved = await _repository.UpdateAsync(provider);
        }

        if (saved.Status != EResultStatus.Success || saved.Data is null)
            return RequestResult<AiProviderResponse>.FromResult(saved);

        if (makeDefault && !saved.Data.IsDefault)
        {
            saved = await _repository.SetDefaultAsync(saved.Data.Id, request.UpdatedBy);
            if (saved.Status != EResultStatus.Success || saved.Data is null)
                return RequestResult<AiProviderResponse>.FromResult(saved);
        }

        await _catalog.ReloadAsync();
        return RequestResult<AiProviderResponse>.Success(
            AiProviderMapper.ToResponse(saved.Data, _secrets.IsSet(saved.Data.ApiKeyEnvVar)));
    }

    private static RequestResult<AiProviderResponse> ValidationFailure(List<(string Field, string Message)> errors)
    {
        var result = RequestResult<AiProviderResponse>.WithValidationError(errors[0].Message, errors[0].Field);
        foreach (var (field, message) in errors.Skip(1))
            result.ValidationResult.AddError(message, field);

        return result;
    }
}
