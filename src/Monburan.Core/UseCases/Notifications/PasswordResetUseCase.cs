using Microsoft.Extensions.Logging;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;

namespace Monburan.Core.UseCases.Notifications;

public sealed class PasswordResetUseCase : IUseCase<PasswordResetRequest, bool>
{
    private readonly SendEmailNotificationUseCase _sendEmailUseCase;
    private readonly ILogger<PasswordResetUseCase> _logger;

    public PasswordResetUseCase(
        SendEmailNotificationUseCase sendEmailUseCase,
        ILogger<PasswordResetUseCase> logger)
    {
        _sendEmailUseCase = sendEmailUseCase;
        _logger = logger;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enviando email de recuperação de senha para {Email}", request.Email);

            var (subject, html) = EmailTemplates.PasswordResetEmail(request.Email, request.ResetLink);

            var emailRequest = new EmailNotificationRequest
            {
                ToEmail = request.Email,
                Subject = subject,
                HtmlContent = html
            };

            var result = await _sendEmailUseCase.ExecuteAsync(emailRequest, cancellationToken);

            if (result.Status == EResultStatus.Success && result.Data)
            {
                _logger.LogInformation("Email de recuperação de senha enviado com sucesso para {Email}", request.Email);
                return RequestResult<bool>.Success(true);
            }

            if (result.Status == EResultStatus.Success)
            {
                _logger.LogWarning("Email de recuperação não entregue para {Email}: provedor indisponível ou não configurado", request.Email);
                return RequestResult<bool>.Success(false);
            }

            _logger.LogWarning("Falha ao enviar email de recuperação de senha para {Email}: {Message}", request.Email, result.Message);
            return RequestResult<bool>.WithError(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar email de recuperação de senha para {Email}", request.Email);
            return RequestResult<bool>.WithError($"Falha ao enviar email de recuperação: {ex.Message}");
        }
    }
}

public class PasswordResetRequest
{
    public string Email { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;
}
