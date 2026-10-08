using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

public interface IIdentityService
{
    Task<UserLoginResponse> Login(UserLoginRequest userLogin);

    Task<UserLoginResponse> LoginWithoutPassword(string userId);

    Task<UserCreateResponse> RegisterUser(UserCreateRequest registrationUser);
}