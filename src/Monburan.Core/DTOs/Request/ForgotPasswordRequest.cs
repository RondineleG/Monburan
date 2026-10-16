using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "The field {0} is mandatory")]
    [EmailAddress(ErrorMessage = "The field {0} is invalid")]
    public string Email { get; set; } = string.Empty;
}
