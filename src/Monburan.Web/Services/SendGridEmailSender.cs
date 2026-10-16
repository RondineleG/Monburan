using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Monburan.Core.Interfaces.Services;

using SendGrid;
using SendGrid.Helpers.Mail;

namespace Monburan.Web.Services;

public sealed class SendGridEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendGridEmailSender> _logger;
    private readonly string? _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailSender(IConfiguration configuration, ILogger<SendGridEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _apiKey = _configuration["SendGrid:ApiKey"];
        _fromEmail = _configuration["SendGrid:FromEmail"] ?? "noreply@monburan.com.br";
        _fromName = _configuration["SendGrid:FromName"] ?? "Monburan";
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("SendGrid API key não configurada. Email não enviado para {ToEmail}: {Subject}", toEmail, subject);
            return false;
        }

        try
        {
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email enviado com sucesso para {ToEmail}: {Subject}", toEmail, subject);
                return true;
            }

            _logger.LogError("Falha ao enviar email para {ToEmail}. Status: {StatusCode}", toEmail, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email para {ToEmail}: {Subject}", toEmail, subject);
            return false;
        }
    }
}
