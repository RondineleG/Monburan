using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.UseCases;

public interface IUseCase<TRequest, TResponse>
{
    Task<RequestResult<TResponse>> ExecuteAsync(TRequest request);
}