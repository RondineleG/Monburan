using Monburan.Core.Interfaces.Services;

namespace Monburan.Web.Services;

/// <summary>
/// <c>Billing:SelfServicePaidPlans</c> (padrão <c>false</c>). Fica desligado em produção até existir cobrança e só vem
/// ligado em Development (appsettings.Development.json), para os testes e a demonstração continuarem trocando de plano.
/// Lê a configuração a cada consulta: mudar a chave não exige reiniciar.
/// </summary>
public sealed class BillingPolicy(IConfiguration configuration) : IBillingPolicy
{
    public bool AllowsSelfServicePaidPlans => configuration.GetValue("Billing:SelfServicePaidPlans", false);
}
