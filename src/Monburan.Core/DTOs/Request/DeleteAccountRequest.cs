namespace Monburan.Core.DTOs.Request;

/// <summary>Exclusão da própria conta pelo app. A senha confirma que é a dona da conta (obrigatória se a conta tiver senha).</summary>
public class DeleteAccountRequest
{
    public string Password { get; set; } = string.Empty;
}
