namespace Monburan.Web.Client
{
    // Add properties to this class and update the server and client AuthenticationStateProviders to
    // expose more information about the authenticated user to the client.
    public class UserInfo
    {
        public string Email { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }
}