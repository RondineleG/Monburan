using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Resumes;
using Monburan.Core.ValueObjects;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Resumes;

public class UpdateResumeOwnershipTests
{
    private readonly IBaseRepository<Resume> _resumes = Substitute.For<IBaseRepository<Resume>>();
    private readonly IEntityValidator<Resume> _validator = Substitute.For<IEntityValidator<Resume>>();

    private UpdateResumeUseCase Sut() => new(_resumes, _validator);

    [Fact]
    public async Task ExecuteAsync_DifferentCandidate_ReturnsNotFoundAndNeverUpdates()
    {
        var resumeId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Resume>.Success(new Resume
            {
                CandidateId = Id.Create(owner),
                Title = "CV do dono"
            }));

        var result = await Sut().ExecuteAsync(new UpdateResumeRequest
        {
            Id = resumeId,
            CandidateId = intruder,
            Title = "Sequestro"
        });

        Assert.Equal(EResultStatus.EntityNotFound, result.Status);
        await _resumes.DidNotReceive().UpdateAsync(Arg.Any<Resume>(), Arg.Any<CancellationToken>());
        _validator.DidNotReceive().ValidateUpdate(Arg.Any<Resume>());
    }
}
