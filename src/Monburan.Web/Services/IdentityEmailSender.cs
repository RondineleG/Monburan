namespace Monburan.Web.Services;

public sealed class IdentityEmailSender(
    Monburan.Core.Interfaces.Services.IEmailSender emailSender)
    : Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(
        ApplicationUser user,
        string email,
        string confirmationLink) =>
        emailSender.SendEmailAsync(
            email,
            "Confirme sua conta Monburan",
            $"<p>Olá!</p><p>Confirme sua conta acessando <a href=\"{confirmationLink}\">este link</a>.</p>");

    public Task SendPasswordResetLinkAsync(
        ApplicationUser user,
        string email,
        string resetLink) =>
        emailSender.SendEmailAsync(
            email,
            "Redefinição de senha Monburan",
            $"<p>Redefina sua senha acessando <a href=\"{resetLink}\">este link</a>.</p>");

    public Task SendPasswordResetCodeAsync(
        ApplicationUser user,
        string email,
        string resetCode) =>
        emailSender.SendEmailAsync(
            email,
            "Código para redefinição de senha Monburan",
            $"<p>Seu código para redefinir a senha é: <strong>{resetCode}</strong></p>");
}
