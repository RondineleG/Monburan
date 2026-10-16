using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Request;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "The field {0} is mandatory")]
    public string RefreshToken { get; set; } = string.Empty;
}
