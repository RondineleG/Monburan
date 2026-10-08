namespace Monburan.Web.Services;

public interface ICurrentUserHelper
{
    Guid? GetCandidateId();
    Guid? GetAdvertiserId();
    string GetUserType();
    bool IsAuthenticated();
}
