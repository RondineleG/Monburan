using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Notifications;

public sealed class SendEmailNotificationUseCase : IUseCase<EmailNotificationRequest, bool>
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendEmailNotificationUseCase> _logger;

    public SendEmailNotificationUseCase(IEmailSender emailSender, ILogger<SendEmailNotificationUseCase> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(EmailNotificationRequest request)
    {
        try
        {
            _logger.LogInformation("Enviando email para {ToEmail} - Assunto: {Subject}", request.ToEmail, request.Subject);

            var sent = await _emailSender.SendEmailAsync(request.ToEmail, request.Subject, request.HtmlContent);

            if (sent)
            {
                _logger.LogInformation("Email enviado com sucesso para {ToEmail}", request.ToEmail);
            }
            else
            {
                _logger.LogWarning("Email não entregue para {ToEmail}: provedor de email indisponível ou não configurado", request.ToEmail);
            }

            return RequestResult<bool>.Success(sent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email para {ToEmail}: {Subject}", request.ToEmail, request.Subject);
            return RequestResult<bool>.WithError($"Falha ao enviar email: {ex.Message}");
        }
    }
}
