namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Regras de cobrança que dependem do ambiente. Enquanto não existe cobrança (gateway ou ativação manual), o anunciante
/// não pode ativar sozinho um plano pago: o servidor recusa, e a tela só esconde o botão.
/// </summary>
public interface IBillingPolicy
{
    /// <summary>O anunciante pode ativar, por conta própria, um plano com preço maior que zero?</summary>
    bool AllowsSelfServicePaidPlans { get; }
}
