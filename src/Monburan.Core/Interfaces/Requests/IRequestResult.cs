using Monburan.Core.Enums;

namespace Monburan.Core.Interfaces.Requests;

public interface IRequestResult
{
    EResultStatus Status { get; }
}