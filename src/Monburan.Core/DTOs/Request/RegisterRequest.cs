using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

/// <summary>
/// Registro de conta via API (mobile). Cria o usuário Identity e o perfil
/// Candidate ou Advertiser conforme <see cref="AccountType"/>.
/// </summary>
public class RegisterRequest
{
    /// <summary>"candidate" ou "advertiser".</summary>
    [Required(ErrorMessage = "The field {0} is mandatory")]
    public string AccountType { get; set; } = string.Empty;

    [Required(ErrorMessage = "The field {0} is mandatory")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The field {0} must have between {2} and {1} characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "The field {0} is mandatory")]
    [EmailAddress(ErrorMessage = "The field {0} is invalid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "The field {0} is mandatory")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "The field {0} must have between {2} and {1} characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "The field {0} is mandatory")]
    [Compare(nameof(Password), ErrorMessage = "Passwords must be equal")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
