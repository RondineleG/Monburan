namespace Monburan.Web.Services;

/// <summary>Ajustes de segurança da aplicação (seção <c>Security</c> do appsettings ou variáveis <c>Security__*</c>).</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public RateLimitSettings RateLimit { get; set; } = new();

    public sealed class RateLimitSettings
    {
        /// <summary>Requisições por segundo de cada IP em todas as rotas (a rajada aceita é o dobro). Protege o site de um IP só.</summary>
        public int RequestsPerSecondPerIp { get; set; } = 50;

        /// <summary>Envios (POST) por minuto de cada IP em login, cadastro e recuperação de senha. Complementa o bloqueio da conta.</summary>
        public int AuthRequestsPerMinutePerIp { get; set; } = 20;
    }
}
