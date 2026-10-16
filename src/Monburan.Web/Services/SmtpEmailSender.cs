using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// Envio de e-mail via SMTP (provedor Resend). Usado quando <c>Smtp:Host</c> está
/// configurado; caso contrário a aplicação cai no <see cref="SendGridEmailSender"/>.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        var host = _configuration["Smtp:Host"];
        var user = _configuration["Smtp:User"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            _logger.LogWarning(
                "SMTP não configurado (Smtp:Host/Smtp:User). Email não enviado para {ToEmail}: {Subject}",
                toEmail, subject);
            return false;
        }

        var port = _configuration.GetValue<int?>("Smtp:Port") ?? 465;
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"]
            ?? _configuration["SendGrid:FromEmail"]
            ?? "noreply@monburan.com.br";
        var fromName = _configuration["Smtp:FromName"]
            ?? _configuration["SendGrid:FromName"]
            ?? "Monburan";

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlContent };

            // 465/2465 = TLS implícito (SSL desde o início); demais = STARTTLS.
            var socketOptions = port is 465 or 2465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, socketOptions);
            await client.AuthenticateAsync(user, password ?? string.Empty);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email enviado com sucesso (SMTP) para {ToEmail}: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email via SMTP para {ToEmail}: {Subject}", toEmail, subject);
            return false;
        }
    }
}
