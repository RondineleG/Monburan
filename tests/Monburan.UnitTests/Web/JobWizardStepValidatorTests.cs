using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class JobWizardStepValidatorTests
{
    private static CreateJobRequest Draft() => new()
    {
        Title = "Atendente de loja",
        Description = "Atendimento ao cliente no caixa e no salão.",
        Type = "OnSite",
        City = "São Paulo",
        State = "SP",
        NumberOfOpenings = 1,
        ExpirationDate = DateTime.UtcNow.AddDays(30)
    };

    [Fact]
    public void Validate_AValidDraft_HasNoErrorsInAnyStep()
    {
        var model = Draft();

        for (var step = 1; step <= JobWizardStepValidator.StepCount; step++)
            JobWizardStepValidator.Validate(step, model).Should().BeEmpty($"a etapa {step} deve aceitar o rascunho válido");
    }

    [Fact]
    public void Validate_StepOne_RequiresTitleAndTheFieldsOfOnSiteJobs()
    {
        var model = Draft();
        model.Title = " ";
        model.Type = "OnSite";
        model.City = "";
        model.State = "SPX";

        var errors = JobWizardStepValidator.Validate(1, model);

        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.Title));
        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.State) && error.Message.Contains("2 letras"));
    }

    [Fact]
    public void Validate_StepOne_SkipsCityAndStateForRemoteJobs()
    {
        var model = Draft();
        model.Type = "Remote";
        model.City = "";
        model.State = "";

        JobWizardStepValidator.Validate(1, model).Should().BeEmpty();
    }

    [Fact]
    public void Validate_StepTwo_RequiresTheDescriptionAndLimitsTheTexts()
    {
        var model = Draft();
        model.Description = " ";
        model.Requirements = new string('x', 2001);

        var errors = JobWizardStepValidator.Validate(2, model);

        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.Description));
        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.Requirements));
    }

    [Fact]
    public void Validate_StepThree_ChecksSalaryRangeOpeningsAndValidity()
    {
        var model = Draft();
        model.SalaryMin = 5000;
        model.SalaryMax = 2000;
        model.NumberOfOpenings = 0;
        model.ExpirationDate = default;

        var errors = JobWizardStepValidator.Validate(3, model);

        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.SalaryMax));
        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.NumberOfOpenings));
        errors.Should().Contain(error => error.Field == nameof(CreateJobRequest.ExpirationDate));
    }

    [Fact]
    public void Validate_StepFour_OnlyReviews_NeverBlocks()
    {
        JobWizardStepValidator.Validate(4, new CreateJobRequest()).Should().BeEmpty();
        JobWizardStepValidator.Validate(0, new CreateJobRequest()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_KeepsTheContractAndPeriodOptional_ForOldJobs()
    {
        var model = Draft();
        model.ContractType = null;
        model.SalaryPeriod = null;

        JobWizardStepValidator.Validate(1, model).Should().BeEmpty();
        JobWizardStepValidator.Validate(3, model).Should().BeEmpty();
    }
}
