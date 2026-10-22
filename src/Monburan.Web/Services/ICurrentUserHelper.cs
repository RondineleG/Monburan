namespace Monburan.Web.Services;

public interface ICurrentUserHelper
{
    Guid? GetCandidateId();
    Guid? GetAdvertiserId();
    string GetUserType();

    /// <summary>Nome de exibição da pessoa logada (claim de nome), ou vazio.</summary>
    string GetDisplayName();
    bool IsAuthenticated();

    /// <summary>Administrador com tudo o que a política de administração exige (inclusive o 2FA, quando obrigatório).</summary>
    bool IsAdmin();
}
