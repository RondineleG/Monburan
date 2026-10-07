using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Requests;

public interface IRequestError : IRequestResult
{
    RequestError? RequestError { get; }
}