using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.Notifications;

public sealed class EmailConfirmationUseCase : IUseCase<EmailConfirmationRequest, bool>
{
    private readonly SendEmailNotificationUseCase _sendEmailUseCase;
    private readonly ILogger<EmailConfirmationUseCase> _logger;

    public EmailConfirmationUseCase(
        SendEmailNotificationUseCase sendEmailUseCase,
        ILogger<EmailConfirmationUseCase> logger)
    {
        _sendEmailUseCase = sendEmailUseCase;
        _logger = logger;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(EmailConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enviando email de confirmação para {Email}", request.Email);

            var (subject, html) = EmailTemplates.WelcomeEmail(request.UserName, request.ConfirmationLink);

            var emailRequest = new EmailNotificationRequest
            {
                ToEmail = request.Email,
                Subject = subject,
                HtmlContent = html
            };

            var result = await _sendEmailUseCase.ExecuteAsync(emailRequest, cancellationToken);

            if (result.Status == EResultStatus.Success && result.Data)
            {
                _logger.LogInformation("Email de confirmação enviado com sucesso para {Email}", request.Email);
                return RequestResult<bool>.Success(true);
            }

            if (result.Status == EResultStatus.Success)
            {
                _logger.LogWarning("Email de confirmação não entregue para {Email}: provedor indisponível ou não configurado", request.Email);
                return RequestResult<bool>.Success(false);
            }

            _logger.LogWarning("Falha ao enviar email de confirmação para {Email}: {Message}", request.Email, result.Message);
            return RequestResult<bool>.WithError(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar email de confirmação para {Email}", request.Email);
            return RequestResult<bool>.WithError($"Falha ao enviar email de confirmação: {ex.Message}");
        }
    }
}

public class EmailConfirmationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ConfirmationLink { get; set; } = string.Empty;
}
