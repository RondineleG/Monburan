using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public class UserCreateRequest
{
    [Compare(nameof(Password), ErrorMessage = "Passwords must be equal")]
    public string ComfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "The field {0} is mandatory")]
    [EmailAddress(ErrorMessage = "The field {0} is invalid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "The field {0} is mandatory")]
    [StringLength(50, ErrorMessage = "The field {0} must have between {2} and {1} characters", MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}