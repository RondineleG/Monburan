using FluentAssertions;
using NSubstitute;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Candidates;

public class InternationalPreferencesUseCaseTests
{
    private readonly ICareerProfileRepository _profiles = Substitute.For<ICareerProfileRepository>();
    private readonly IFeatureFlags _flags = Substitute.For<IFeatureFlags>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly GetInternationalPreferencesUseCase _get;
    private readonly UpsertInternationalPreferencesUseCase _upsert;

    public InternationalPreferencesUseCaseTests()
    {
        _flags.IsEnabledAsync("carreira-internacional", Arg.Any<System.Security.Claims.ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _profiles.CreateAsync(Arg.Any<CareerProfile>())
            .Returns(call => RequestResult<CareerProfile>.Success(call.Arg<CareerProfile>()));
        _profiles.UpdateAsync(Arg.Any<CareerProfile>())
            .Returns(call => RequestResult<CareerProfile>.Success(call.Arg<CareerProfile>()));

        _get = new GetInternationalPreferencesUseCase(_profiles, _flags);
        _upsert = new UpsertInternationalPreferencesUseCase(_profiles, _flags);
    }

    private static InternationalPreferencesRequest ValidRequest() => new()
    {
        InterestedCountries = ["es", "pt", "DE"],
        Mobility = EInternationalMobility.RemoteOrRelocation,
        WorkAuthorizationStatus = EWorkAuthorizationStatus.WorkVisa
    };

    [Fact]
    public async Task Get_WhenTheCandidateHasNoProfile_ReturnsNotInformedDefaults()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));

        var result = await _get.ExecuteAsync(new GetInternationalPreferencesCommand(_candidateId), user: null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.InterestedCountries.Should().BeEmpty();
        result.Data.Mobility.Should().Be(EInternationalMobility.NotInformed);
        result.Data.WorkAuthorizationStatus.Should().Be(EWorkAuthorizationStatus.NotInformed);
    }

    [Fact]
    public async Task Upsert_CreatesProfileWithNormalizedCountryCodes()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));

        var result = await _upsert.ExecuteAsync(
            new UpsertInternationalPreferencesCommand(_candidateId, ValidRequest()), user: null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.InterestedCountries.Should().Equal("ES", "PT", "DE");
        result.Data.Mobility.Should().Be(EInternationalMobility.RemoteOrRelocation);
        await _profiles.Received(1).CreateAsync(Arg.Is<CareerProfile>(p =>
            p.CandidateId.Value == _candidateId && p.TenantId == "default"));
    }

    [Fact]
    public async Task Upsert_PreservesExistingNationalFields()
    {
        var existing = new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            DesiredRole = "Dev",
            City = "Curitiba",
            State = "PR"
        };
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(existing));

        var result = await _upsert.ExecuteAsync(
            new UpsertInternationalPreferencesCommand(_candidateId, ValidRequest()), user: null);

        result.Status.Should().Be(EResultStatus.Success);
        await _profiles.Received(1).UpdateAsync(Arg.Is<CareerProfile>(p =>
            p.DesiredRole == "Dev" && p.City == "Curitiba" && p.Mobility == EInternationalMobility.RemoteOrRelocation));
    }

    [Fact]
    public async Task Upsert_WhenFlagIsDisabled_Rejects()
    {
        _flags.IsEnabledAsync("carreira-internacional", Arg.Any<System.Security.Claims.ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _upsert.ExecuteAsync(
            new UpsertInternationalPreferencesCommand(_candidateId, ValidRequest()), user: null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _profiles.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory]
    [InlineData("ESP")]
    [InlineData("E1")]
    [InlineData("E")]
    [InlineData(" es1")]
    public async Task Upsert_WhenCountryCodeIsInvalid_Rejects(string code)
    {
        var request = ValidRequest();
        request.InterestedCountries = [code];

        var result = await _upsert.ExecuteAsync(
            new UpsertInternationalPreferencesCommand(_candidateId, request), user: null);

        result.Status.Should().Be(EResultStatus.HasValidation);
    }

    [Fact]
    public async Task Upsert_WhenTooManyCountries_Rejects()
    {
        var request = ValidRequest();
        request.InterestedCountries = Enumerable.Range(0, 21).Select(i => $"C{i:D}").ToList();
        // códigos inválidos de propósito não: ajusta para formato válido
        request.InterestedCountries = Enumerable.Range(0, 21).Select(i => $"E{i:X}").Where(c => c.Length == 2).ToList();
        request.InterestedCountries = Enumerable.Range(0, 21).Select(i => ((char)('A' + i % 26)).ToString() + ((char)('A' + (i + 1) % 26)).ToString()).ToList();

        var result = await _upsert.ExecuteAsync(
            new UpsertInternationalPreferencesCommand(_candidateId, request), user: null);

        result.Status.Should().Be(EResultStatus.HasValidation);
    }

    [Fact]
    public async Task Upsert_DeduplicatesCountries()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));
        var request = ValidRequest();
        request.InterestedCountries = ["es", "ES", " pt "];

        var result = await _upsert.ExecuteAsync(
            new UpsertInternationalPreferencesCommand(_candidateId, request), user: null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.InterestedCountries.Should().Equal("ES", "PT");
    }
}

public class CandidateLanguagesUseCaseTests
{
    private readonly ICandidateLanguageRepository _languages = Substitute.For<ICandidateLanguageRepository>();
    private readonly IFeatureFlags _flags = Substitute.For<IFeatureFlags>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly GetCandidateLanguagesUseCase _get;
    private readonly ReplaceCandidateLanguagesUseCase _replace;

    public CandidateLanguagesUseCaseTests()
    {
        _flags.IsEnabledAsync("carreira-internacional", Arg.Any<System.Security.Claims.ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _languages.ReplaceForCandidateAsync(Arg.Any<Id>(), Arg.Any<IReadOnlyList<CandidateLanguage>>())
            .Returns(call => RequestResult<IEnumerable<CandidateLanguage>>.Success(call.Arg<IReadOnlyList<CandidateLanguage>>()));

        _get = new GetCandidateLanguagesUseCase(_languages, _flags);
        _replace = new ReplaceCandidateLanguagesUseCase(_languages, _flags);
    }

    [Fact]
    public async Task Get_WhenEmpty_ReturnsEmptyList()
    {
        _languages.GetByCandidateIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<IEnumerable<CandidateLanguage>>.Success([]));

        var result = await _get.ExecuteAsync(new GetCandidateLanguagesCommand(_candidateId), user: null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Languages.Should().BeEmpty();
    }

    [Fact]
    public async Task Replace_NormalizesBcp47Tags()
    {
        var request = new CandidateLanguagesRequest
        {
            Languages =
            [
                new CandidateLanguageItem { LanguageTag = "es-es", Level = ELanguageLevel.Advanced, Evidence = ELanguageEvidence.SelfDeclared },
                new CandidateLanguageItem { LanguageTag = "EN-us", Level = ELanguageLevel.Intermediate, Evidence = ELanguageEvidence.Certificate },
                new CandidateLanguageItem { LanguageTag = "pt", Level = ELanguageLevel.Native, Evidence = ELanguageEvidence.Native }
            ]
        };

        var result = await _replace.ExecuteAsync(new ReplaceCandidateLanguagesCommand(_candidateId, request), user: null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Languages.Select(l => l.LanguageTag).Should().Equal("es-ES", "en-US", "pt");
        await _languages.Received(1).ReplaceForCandidateAsync(
            Arg.Is<Id>(id => id.Value == _candidateId),
            Arg.Is<IReadOnlyList<CandidateLanguage>>(list => list.Count == 3));
    }

    [Theory]
    [InlineData("espanol")]
    [InlineData("e")]
    [InlineData("es_ES")]
    [InlineData("es-ESP")]
    public async Task Replace_WhenTagIsInvalid_Rejects(string tag)
    {
        var request = new CandidateLanguagesRequest
        {
            Languages = [new CandidateLanguageItem { LanguageTag = tag, Level = ELanguageLevel.Basic, Evidence = ELanguageEvidence.SelfDeclared }]
        };

        var result = await _replace.ExecuteAsync(new ReplaceCandidateLanguagesCommand(_candidateId, request), user: null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _languages.DidNotReceiveWithAnyArgs().ReplaceForCandidateAsync(default!, default!);
    }

    [Fact]
    public async Task Replace_WhenTagIsDuplicated_Rejects()
    {
        var request = new CandidateLanguagesRequest
        {
            Languages =
            [
                new CandidateLanguageItem { LanguageTag = "es-ES", Level = ELanguageLevel.Basic, Evidence = ELanguageEvidence.SelfDeclared },
                new CandidateLanguageItem { LanguageTag = "ES-es", Level = ELanguageLevel.Fluent, Evidence = ELanguageEvidence.Native }
            ]
        };

        var result = await _replace.ExecuteAsync(new ReplaceCandidateLanguagesCommand(_candidateId, request), user: null);

        result.Status.Should().Be(EResultStatus.HasValidation);
    }

    [Fact]
    public async Task Replace_WhenFlagIsDisabled_Rejects()
    {
        _flags.IsEnabledAsync("carreira-internacional", Arg.Any<System.Security.Claims.ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _replace.ExecuteAsync(
            new ReplaceCandidateLanguagesCommand(_candidateId, new CandidateLanguagesRequest()), user: null);

        result.Status.Should().Be(EResultStatus.HasValidation);
    }
}
