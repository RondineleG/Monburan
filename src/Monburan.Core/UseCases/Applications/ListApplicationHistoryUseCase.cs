using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Applications;

public sealed record ListApplicationHistoryRequest(Guid ApplicationId, Guid? CandidateId, Guid? AdvertiserId);

/// <summary>Histórico de etapas da candidatura, da mais antiga para a mais nova. Visível ao candidato dono e ao anunciante dono da vaga.</summary>
public class ListApplicationHistoryUseCase : IUseCase<ListApplicationHistoryRequest, IEnumerable<ApplicationHistoryItemResponse>>
{
    private readonly IBaseRepository<Application> _applicationRepository;
    private readonly IApplicationStatusHistoryRepository _historyRepository;

    public ListApplicationHistoryUseCase(
        IBaseRepository<Application> applicationRepository,
        IApplicationStatusHistoryRepository historyRepository)
    {
        _applicationRepository = applicationRepository;
        _historyRepository = historyRepository;
    }

    public async Task<RequestResult<IEnumerable<ApplicationHistoryItemResponse>>> ExecuteAsync(ListApplicationHistoryRequest request)
    {
        var applicationResult = await _applicationRepository.GetByIdAsync(Id.Create(request.ApplicationId));
        if (applicationResult.Status != EResultStatus.Success || applicationResult.Data is null)
            return RequestResult<IEnumerable<ApplicationHistoryItemResponse>>.EntityNotFound(
                "Application", request.ApplicationId, "Candidatura não encontrada.");

        var application = applicationResult.Data;
        if (!IsOwner(application, request))
        {
            return RequestResult<IEnumerable<ApplicationHistoryItemResponse>>.EntityNotFound(
                "Application", request.ApplicationId, "Candidatura não encontrada.");
        }

        var historyResult = await _historyRepository.ListByApplicationAsync(request.ApplicationId);
        if (historyResult.Status != EResultStatus.Success || historyResult.Data is null)
            return RequestResult<IEnumerable<ApplicationHistoryItemResponse>>.FromResult(historyResult);

        return RequestResult<IEnumerable<ApplicationHistoryItemResponse>>.Success(
            historyResult.Data.Select(item => new ApplicationHistoryItemResponse
            {
                Status = item.Status.ToString(),
                Date = item.Date,
                Note = item.Note,
                Responsible = item.Responsible
            }).ToList());
    }

    private static bool IsOwner(Application application, ListApplicationHistoryRequest request)
    {
        var isCandidateOwner = request.CandidateId.HasValue && application.CandidateId.Value == request.CandidateId.Value;
        var isAdvertiserOwner = request.AdvertiserId.HasValue
            && application.Job is not null
            && application.Job.AdvertiserId.Value == request.AdvertiserId.Value;

        return isCandidateOwner || isAdvertiserOwner;
    }
}
