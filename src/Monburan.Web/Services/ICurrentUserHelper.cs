namespace Monburan.Web.Services;

public interface ICurrentUserHelper
{
    Guid? GetCandidateId();
    Guid? GetAdvertiserId();
    string GetUserType();
    bool IsAuthenticated();

    /// <summary>Administrador com tudo o que a política de administração exige (inclusive o 2FA, quando obrigatório).</summary>
    bool IsAdmin();
}
