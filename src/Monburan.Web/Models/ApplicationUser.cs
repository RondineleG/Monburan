namespace Monburan.Web.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public DateTime? CreatedAt { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = false;

        public DateTime? LastLoginAt { get; set; }

        public string LastName { get; set; } = string.Empty;

        public byte[] ProfilePicture { get; set; } = [];
    }
}