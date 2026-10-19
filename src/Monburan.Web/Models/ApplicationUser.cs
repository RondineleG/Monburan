namespace Monburan.Web.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public DateTime? CreatedAt { get; set; }

        public string FirstName { get; set; } = string.Empty;

        /// <summary>Conta ativa. Inativada pela administração, não entra nem mantém sessão (ver <see cref="Services.ApplicationSignInManager"/>).</summary>
        public bool IsActive { get; set; } = true;

        public DateTime? DeactivatedAt { get; set; }

        /// <summary>E-mail do administrador que inativou a conta.</summary>
        public string? DeactivatedBy { get; set; }

        public string? DeactivationReason { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string LastName { get; set; } = string.Empty;

        public byte[] ProfilePicture { get; set; } = [];
    }
}