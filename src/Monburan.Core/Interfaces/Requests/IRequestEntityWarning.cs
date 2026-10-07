using Monburan.Core.Requests;

namespace Monburan.Core.Interfaces.Requests;

public interface IRequestEntityWarning : IRequestResult
{
    RequestEntityWarning? RequestEntityWarning { get; }
}