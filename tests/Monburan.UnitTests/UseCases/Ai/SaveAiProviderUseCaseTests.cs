using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.UseCases.Ai;
using Monburan.UnitTests.Web;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Ai;

public class SaveAiProviderUseCaseTests
{
    private readonly FakeAiProviderRepository _repository = new();
    private readonly IAiProviderCatalog _catalog = Substitute.For<IAiProviderCatalog>();
    private readonly IAiSecretReader _secrets = Substitute.For<IAiSecretReader>();
    private readonly SaveAiProviderUseCase _useCase;

    public SaveAiProviderUseCaseTests()
    {
        _useCase = new SaveAiProviderUseCase(_repository, _catalog, _secrets);
    }

    private static AiProviderRequest Request(Action<AiProviderRequest>? change = null)
    {
        var request = new AiProviderRequest
        {
            Name = "OpenRouter",
            Protocol = EAiProtocol.OpenAiCompatible,
            BaseUrl = "https://openrouter.ai/api/v1",
            Model = "deepseek/deepseek-chat",
            ApiKeyEnvVar = "AI_KEY_OPENROUTER",
            IsEnabled = true,
            UpdatedBy = "admin@monburan.com.br"
        };
        change?.Invoke(request);
        return request;
    }

    [Fact]
    public async Task Create_TheFirstEnabledProvider_BecomesTheDefaultOnItsOwn()
    {
        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsDefault.Should().BeTrue();
        _repository.Items.Should().ContainSingle().Which.IsDefault.Should().BeTrue();
        _repository.LastSetDefaultBy.Should().Be("admin@monburan.com.br");
    }

    [Fact]
    public async Task Create_TheFirstProviderDisabled_LeavesTheAiWithoutADefault()
    {
        var result = await _useCase.ExecuteAsync(Request(r => r.IsEnabled = false));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsDefault.Should().BeFalse();
        _repository.SetDefaultCalls.Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenOtherProvidersExist_KeepsTheCurrentDefault()
    {
        var standard = _repository.Add(AiTestData.Entity("Padrão", isDefault: true));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsDefault.Should().BeFalse();
        standard.IsDefault.Should().BeTrue();
        _repository.SetDefaultCalls.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithMakeDefault_MovesTheDefaultToTheNewProvider()
    {
        var standard = _repository.Add(AiTestData.Entity("Padrão", isDefault: true));

        var result = await _useCase.ExecuteAsync(Request(r => r.MakeDefault = true));

        result.Data!.IsDefault.Should().BeTrue();
        standard.IsDefault.Should().BeFalse();
        _repository.Items.Count(provider => provider.IsDefault).Should().Be(1);
    }

    [Fact]
    public async Task Create_TrimsAndNormalizesWhatWasTyped_AndRecordsWhoDidIt()
    {
        var result = await _useCase.ExecuteAsync(Request(r =>
        {
            r.Name = "  OpenRouter  ";
            r.BaseUrl = "  https://openrouter.ai/api/v1//  ";
            r.Model = " deepseek/deepseek-chat ";
            r.ApiKeyEnvVar = " AI_KEY_OPENROUTER ";
        }));

        result.Status.Should().Be(EResultStatus.Success);
        var saved = _repository.Items.Should().ContainSingle().Which;
        saved.Name.Should().Be("OpenRouter");
        saved.BaseUrl.Should().Be("https://openrouter.ai/api/v1");
        saved.Model.Should().Be("deepseek/deepseek-chat");
        saved.ApiKeyEnvVar.Should().Be("AI_KEY_OPENROUTER");
        saved.CreatedBy.Should().Be("admin@monburan.com.br");
        saved.UpdatedBy.Should().Be("admin@monburan.com.br");
        saved.TenantId.Should().Be("default");
    }

    [Fact]
    public async Task Create_WithInvalidData_ReturnsTheErrorsByFieldAndSavesNothing()
    {
        var result = await _useCase.ExecuteAsync(Request(r =>
        {
            r.Name = " ";
            r.BaseUrl = "http://api.exemplo.com/v1";
            r.Model = "modelo com espaço";
            r.ApiKeyEnvVar = "JWT_SIGNING_KEY";
        }));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Select(error => error.Field)
            .Should().BeEquivalentTo(nameof(AiProviderRequest.Name), nameof(AiProviderRequest.BaseUrl), nameof(AiProviderRequest.Model), nameof(AiProviderRequest.ApiKeyEnvVar));
        _repository.CreateCalls.Should().Be(0);
        await _catalog.DidNotReceive().ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("OpenRouter")]
    [InlineData("openrouter")]
    [InlineData("  OPENROUTER ")]
    public async Task Create_WithADuplicateName_IsRejectedIgnoringCase(string name)
    {
        _repository.Add(AiTestData.Entity("OpenRouter"));

        var result = await _useCase.ExecuteAsync(Request(r => r.Name = name));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(AiProviderRequest.Name) && error.Message.Contains("Já existe"));
        _repository.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Create_ADisabledProviderCannotBeTheDefault()
    {
        var result = await _useCase.ExecuteAsync(Request(r =>
        {
            r.IsEnabled = false;
            r.MakeDefault = true;
        }));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == nameof(AiProviderRequest.MakeDefault));
        _repository.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Edit_UpdatesTheSameProvider_AndKeepsItAsTheDefault()
    {
        var existing = _repository.Add(AiTestData.Entity("Padrão", isDefault: true));

        var result = await _useCase.ExecuteAsync(Request(r =>
        {
            r.Id = existing.Id.Value;
            r.Name = "Padrão renomeado";
            r.Model = "outro-modelo";
        }));

        result.Status.Should().Be(EResultStatus.Success);
        _repository.Items.Should().ContainSingle();
        _repository.UpdateCalls.Should().Be(1);
        existing.Name.Should().Be("Padrão renomeado");
        existing.Model.Should().Be("outro-modelo");
        existing.IsDefault.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_KeepingTheSameNameIsNotADuplicate()
    {
        var existing = _repository.Add(AiTestData.Entity("OpenRouter"));

        var result = await _useCase.ExecuteAsync(Request(r => r.Id = existing.Id.Value));

        result.Status.Should().Be(EResultStatus.Success);
    }

    [Fact]
    public async Task Edit_OfAProviderThatDoesNotExist_ReturnsNotFound()
    {
        var result = await _useCase.ExecuteAsync(Request(r => r.Id = Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        _repository.UpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Edit_TheDefaultProviderCannotBeDisabled()
    {
        var standard = _repository.Add(AiTestData.Entity("Padrão", isDefault: true));

        var result = await _useCase.ExecuteAsync(Request(r =>
        {
            r.Id = standard.Id.Value;
            r.Name = "Padrão";
            r.IsEnabled = false;
        }));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == nameof(AiProviderRequest.IsEnabled));
        standard.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_ANonDefaultProviderCanBeDisabled()
    {
        _repository.Add(AiTestData.Entity("Padrão", isDefault: true));
        var other = _repository.Add(AiTestData.Entity("Outro"));

        var result = await _useCase.ExecuteAsync(Request(r =>
        {
            r.Id = other.Id.Value;
            r.Name = "Outro";
            r.IsEnabled = false;
        }));

        result.Status.Should().Be(EResultStatus.Success);
        other.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Save_ReloadsTheCatalog_SoTheChangeAppliesRightAway()
    {
        await _useCase.ExecuteAsync(Request());

        await _catalog.Received(1).ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_ReportsWhetherTheKeyExistsOnTheServer_NeverTheKey(bool keyIsSet)
    {
        _secrets.IsSet("AI_KEY_OPENROUTER").Returns(keyIsSet);

        var result = await _useCase.ExecuteAsync(Request());

        result.Data!.KeyConfigured.Should().Be(keyIsSet);
        result.Data.ApiKeyEnvVar.Should().Be("AI_KEY_OPENROUTER");
        _secrets.DidNotReceive().Get(Arg.Any<string>());
    }
}
