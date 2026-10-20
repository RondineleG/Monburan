using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Resumes;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Resumes;

public class ResumeImportTests
{
    // Texto real extraído de um PDF de currículo (com pdftotext) — mesmo formato que o PdfPig devolve
    private const string SampleCvText = """
        Rondinele Guimarães
        Desenvolvedor Backend C# | .NET | SQL Server | Mensageria | Sistemas Distribuídos
        R. Jacob Faintyck, 325 - Uvaranas, Ponta Grossa - PR | (47) 99005-7056 | rondineleg@gmail.com | linkedin.com/in/
        rondineleg | github.com/RondineleG

        RESUMO
        Desenvolvedor Backend com experiência em C# e .NET, atuando no desenvolvimento e manutenção de sistemas,
        correção de bugs e implementação de melhorias. Experiência com microsserviços, sistemas distribuídos e mensageria
        aplicados ao domínio de crédito consignado público (INSS) e crédito privado (CLT). Comprometido com a entrega de
        soluções funcionais e estáveis, buscando aprendizado contínuo e contribuindo para os resultados da equipe.

        PRINCIPAIS COMPETÊNCIAS
        C# | .NET Core/.NET 5/6/8 | ASP.NET Core | Entity Framework | SQL Server | Microsserviços | RabbitMQ | Amazon SQS/
        SNS/S3 | APIs RESTful | Git | Docker | Angular | Javascript/TypeScript

        EXPERIÊNCIA PROFISSIONAL
        Banco Pine

        março de 2026 - atualmente

        Desenvolvedor Backend C# | .NET | SQL Server | RabbitMQ | Microsserviços
        São Paulo, Brasil

        Desenvolvedor Backend atuando em sistemas distribuídos baseados em microsserviços com mensageria via RabbitMQ, no
        contexto de crédito consignado público (averbações via INSS) e crédito privado (CLT). Responsável pelo desenvolvimento e
        manutenção de serviços de autorização e processamento de propostas, integrações entre serviços e otimizações em banco
        de dados SQL Server.
        Tecnologias: C#, .NET, ASP.NET Core, Entity Framework, SQL Server, RabbitMQ, Microsserviços, APIs RESTful, Git, Docker

        Lexos by TOTVS

        julho de 2025 - fevereiro de 2026

        Desenvolvedor Backend C# | .NET | .NET Core | SQL Server | Javascript/Typescript | Angular
        Brasil

        Desenvolvedor Backend atuando como N3 na resolução de bugs e implementação de pequenas melhorias em sistemas de
        integração com plataformas de venda.
        Tecnologias: C#, .NET 5/6/8, ASP.NET Core, Entity Framework, Angular, SQL Server, Git, Amazon SQS

        Viveo

        abril de 2024 - julho de 2025 (1 ano 4 meses)

        Desenvolvedor Backend C# | .NET Core | SQL Server | Aspnet MVC | Javascript
        São Paulo, Brasil

        Desenvolvedor Backend C# responsável pelo desenvolvimento e manutenção de sistemas web.
        Tecnologias: C#, ASP.NET Core, Entity Framework, Javascript/TypeScript, Git, MVC, SQL Server, XML

        FORMAÇÃO ACADÊMICA
        Universidade do Vale do Itajaí | Univali
        Curso Superior de Tecnologia (CST) — Análise e Desenvolvimento de Sistemas

        janeiro de 2022 - dezembro de 2026
        """;

    [Fact]
    public void Heuristics_Parse_PopulaSecoesDoCurriculoReal()
    {
        var draft = ResumeTextHeuristics.Parse(SampleCvText, "Rondinele_CV.pdf");

        draft.ExtractedBy.Should().Be(EResumeImportMode.Local);
        draft.Title.Should().Be("Desenvolvedor Backend C#");
        draft.Summary.Should().Contain("microsserviços");

        draft.Experiences.Should().HaveCount(3);
        var pine = draft.Experiences[0];
        pine.Company.Should().Be("Banco Pine");
        pine.Position.Should().Contain("Desenvolvedor Backend");
        pine.IsCurrent.Should().BeTrue();
        pine.EndDate.Should().BeNull();
        pine.StartDate.Should().Be(new DateTime(2026, 3, 1));

        var lexos = draft.Experiences[1];
        lexos.Company.Should().Be("Lexos by TOTVS");
        lexos.StartDate.Should().Be(new DateTime(2025, 7, 1));
        lexos.EndDate.Should().Be(new DateTime(2026, 2, 1));

        draft.Education.Should().HaveCount(1);
        draft.Education[0].Institution.Should().Contain("Universidade do Vale do Itajaí");
        draft.Education[0].Level.Should().Be(EEducationLevel.Technical);
        draft.Education[0].StartDate.Should().Be(new DateTime(2022, 1, 1));

        var skills = draft.Skills.Select(s => s.Skill).ToList();
        skills.Should().Contain(["C#", ".NET", "ASP.NET Core", "Entity Framework", "SQL Server", "RabbitMQ", "Docker", "Git", "Angular"]);
        skills.Should().Contain(s => s.StartsWith("Amazon SQS"));

        draft.MissingFields.Should().Contain("languages");
    }

    [Fact]
    public void Heuristics_Parse_PdfSemTexto_RetornaRascunhoVazio()
    {
        var draft = ResumeTextHeuristics.Parse(string.Empty, "cv.pdf");
        draft.Experiences.Should().BeEmpty();
        ResumeTextHeuristics.MissingFieldsOf(draft).Should().Contain(["summary", "experiences", "education", "languages", "skills"]);
    }

    [Theory]
    [InlineData("março de 2026", 2026, 3)]
    [InlineData("julho de 2025", 2025, 7)]
    [InlineData("03/2024", 2024, 3)]
    [InlineData("2024-03", 2024, 3)]
    [InlineData("2022", 2022, 1)]
    public void Heuristics_ParseDate_FormatoaPtBr(string value, int year, int month)
    {
        ResumeTextHeuristics.ParseDate(value).Should().Be(new DateTime(year, month, 1));
    }

    [Fact]
    public void Heuristics_TryParseDateRange_Atualmente()
    {
        var ok = ResumeTextHeuristics.TryParseDateRange("março de 2026 - atualmente", out var start, out var end, out var current);
        ok.Should().BeTrue();
        start.Should().Be(new DateTime(2026, 3, 1));
        end.Should().BeNull();
        current.Should().BeTrue();
    }

    [Fact]
    public void SkillNormalizer_MapeiaApelidosParaCanonico()
    {
        SkillNormalizer.Normalize("csharp").Should().Be("C#");
        SkillNormalizer.Normalize("dotnet core").Should().Be(".NET");
        SkillNormalizer.Normalize(" .net core ").Should().Be(".NET");
        SkillNormalizer.Normalize("sqlserver").Should().Be("SQL Server");
        SkillNormalizer.Normalize("rest api").Should().Be("APIs RESTful");
        SkillNormalizer.Normalize("Habilidade Nova").Should().Be("Habilidade Nova");
    }

    [Fact]
    public void SkillNormalizer_NormalizeAll_RemoverDuplicatasIgnorandoCaixa()
    {
        var skills = SkillNormalizer.NormalizeAll(["C#", "csharp", "Docker", "docker", "", null]);
        skills.Should().Equal(["C#", "Docker"]);
    }

    [Fact]
    public void ImportParser_JsonValido_MontaRascunhoCompleto()
    {
        const string json = """
            {"title": "Dev Backend .NET",
             "summary": "Resumo pronto.",
             "experiences": [{"company": "Banco X", "position": "Dev", "description": "Coisas.", "startDate": "2024-03", "endDate": null, "isCurrent": true}],
             "education": [{"institution": "Univali", "course": "ADS", "level": "technical", "status": "ongoing", "startDate": "2022-01", "endDate": "2026-12"}],
             "languages": [{"name": "Inglês", "level": "advanced"}],
             "skills": [{"name": "csharp", "level": 4, "yearsOfExperience": 3}, "docker"]}
            """;

        ResumeImportParser.TryParse(json, out var draft).Should().BeTrue();
        draft.Should().NotBeNull();
        draft!.ExtractedBy.Should().Be(EResumeImportMode.Ai);
        draft.Title.Should().Be("Dev Backend .NET");
        draft.Experiences.Should().ContainSingle();
        draft.Experiences[0].StartDate.Should().Be(new DateTime(2024, 3, 1));
        draft.Experiences[0].IsCurrent.Should().BeTrue();
        draft.Education[0].Level.Should().Be(EEducationLevel.Technical);
        draft.Education[0].Status.Should().Be(EEducationStatus.Ongoing);
        draft.Languages[0].Level.Should().Be(ELanguageLevel.Advanced);
        draft.Skills.Select(s => s.Skill).Should().Equal(["C#", "Docker"]);
    }

    [Fact]
    public void ImportParser_JsonCercadoDeTexto_AindaFunciona()
    {
        ResumeImportParser.TryParse("Aqui está: {\"title\": \"Dev\", \"skills\": []} fim.", out var draft)
            .Should().BeTrue();
        draft!.Title.Should().Be("Dev");
    }

    [Fact]
    public void ImportParser_LixoNaoJson_RetornaFalso()
    {
        ResumeImportParser.TryParse("não é json", out _).Should().BeFalse();
    }
}

public class ImportResumeFromPdfUseCaseTests
{
    private readonly IPdfTextExtractor _extractor = Substitute.For<IPdfTextExtractor>();
    private readonly IAiTextClient _ai = Substitute.For<IAiTextClient>();
    private readonly IAiUsageLimiter _limiter = Substitute.For<IAiUsageLimiter>();
    private readonly IBaseRepository<Resume> _resumes = Substitute.For<IBaseRepository<Resume>>();
    private readonly IEntityValidator<Resume> _validator = Substitute.For<IEntityValidator<Resume>>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly ImportResumeFromPdfUseCase _useCase;

    public ImportResumeFromPdfUseCaseTests()
    {
        _validator.Validate(Arg.Any<Resume>()).Returns(ValidationResult.Success());
        _resumes.CreateAsync(Arg.Any<Resume>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<Resume>.Success(call.Arg<Resume>()));
        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(call =>
        {
            call[1] = TimeSpan.Zero;
            return true;
        });

        _useCase = new ImportResumeFromPdfUseCase(
            _extractor, _ai, _limiter, new CreateResumeUseCase(_resumes, _validator));
    }

    private ImportResumeRequest Request(bool consent = false) => new()
    {
        FileStream = new MemoryStream([1, 2, 3]),
        FileName = "cv.pdf",
        FileUrl = "/uploads/cv.pdf",
        CandidateId = _candidateId,
        AcceptsExternalProcessing = consent
    };

    [Fact]
    public async Task PdfSemTexto_CriaCurriculoMinimoEMarcaNenhumaExtracao()
    {
        _extractor.ExtractText(Arg.Any<Stream>()).Returns(string.Empty);

        var result = await _useCase.ExecuteAsync(Request(consent: true));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ExtractedBy.Should().Be(EResumeImportMode.None);
        result.Data.MissingFields.Should().Contain(["summary", "experiences", "skills"]);
        await _resumes.Received(1).CreateAsync(
            Arg.Is<Resume>(r => r.SourceFileName == "cv.pdf" && r.SourceFileUrl == "/uploads/cv.pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SemConsentimento_UsaLeituraLocalENaoChamaIa()
    {
        _ai.IsConfigured.Returns(true);
        _extractor.ExtractText(Arg.Any<Stream>()).Returns("RESUMO\nDev backend\n\nHABILIDADES\nC# | Docker");

        var result = await _useCase.ExecuteAsync(Request(consent: false));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ExtractedBy.Should().Be(EResumeImportMode.Local);
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
        await _resumes.Received(1).CreateAsync(
            Arg.Is<Resume>(r => r.Skills.Any(s => s.Skill == "C#")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComConsentimentoEIa_UsaJsonDaIa()
    {
        _ai.IsConfigured.Returns(true);
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion("{\"title\": \"Dev .NET\", \"skills\": [{\"name\": \"csharp\", \"level\": 4}]}", "m", 1, 1));
        _extractor.ExtractText(Arg.Any<Stream>()).Returns("texto qualquer do pdf");

        var result = await _useCase.ExecuteAsync(Request(consent: true));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ExtractedBy.Should().Be(EResumeImportMode.Ai);
        await _resumes.Received(1).CreateAsync(
            Arg.Is<Resume>(r => r.Title == "Dev .NET" && r.Skills.Any(s => s.Skill == "C#")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IaFalha_CaiNaLeituraLocal()
    {
        _ai.IsConfigured.Returns(true);
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AiCompletion>(new AiClientException(AiFailureKind.Timeout, "timeout")));
        _extractor.ExtractText(Arg.Any<Stream>()).Returns("HABILIDADES\nDocker");

        var result = await _useCase.ExecuteAsync(Request(consent: true));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ExtractedBy.Should().Be(EResumeImportMode.Local);
    }

    [Fact]
    public async Task StreamNaoSeekavel_FazBufferAntesDeExtrair()
    {
        // IBrowserFile.OpenReadStream pode devolver stream sem Seek; o use case deve copiar
        // para a memória e entregar um stream seekável ao extrator
        _extractor.ExtractText(Arg.Do<Stream>(s => s.CanSeek.Should().BeTrue()))
            .Returns("HABILIDADES\nDocker");

        var request = Request();
        request.FileStream = new NonSeekableStream([1, 2, 3]);

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _resumes.Received(1).CreateAsync(Arg.Any<Resume>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PdfCorrompido_ErroAmigavelENaoCriaCurriculo()
    {
        _extractor.ExtractText(Arg.Any<Stream>())
            .Returns(call => throw new PdfExtractionException("corrompido"));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        await _resumes.DidNotReceive().CreateAsync(Arg.Any<Resume>(), Arg.Any<CancellationToken>());
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Position { get => base.Position; set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}

public class ResumeImportPromptTests
{
    [Fact]
    public void SystemPrompt_DoesNotInventSummaryOrMixExperiences()
    {
        ResumeImportPrompt.SystemPrompt.Should()
            .Contain("devolva uma string vazia")
            .And.Contain("importar não é inventar")
            .And.Contain("empresas diferentes");
    }
}
