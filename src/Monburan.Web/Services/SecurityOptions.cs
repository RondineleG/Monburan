namespace Monburan.Web.Services;

/// <summary>Ajustes de segurança da aplicação (seção <c>Security</c> do appsettings ou variáveis <c>Security__*</c>).</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Política de conteúdo (CSP) só em modo de aviso: o navegador registra as violações no console, mas não bloqueia.
    /// Válvula de emergência para uma política nova que quebre alguma página; o normal é <c>false</c> (bloqueia).
    /// </summary>
    public bool CspReportOnly { get; set; }

    /// <summary>
    /// Administradores só abrem as áreas de administração com a verificação em duas etapas (2FA) ligada na conta; sem ela são
    /// levados à tela de configuração. Desligado só em desenvolvimento. Se o único administrador perder o aparelho e os códigos
    /// de recuperação, <c>Security__RequireTwoFactorForAdmins=false</c> reabre o acesso até ele reconfigurar.
    /// </summary>
    public bool RequireTwoFactorForAdmins { get; set; } = true;

    public RateLimitSettings RateLimit { get; set; } = new();

    public sealed class RateLimitSettings
    {
        /// <summary>Requisições por segundo de cada IP em todas as rotas (a rajada aceita é o dobro). Protege o site de um IP só.</summary>
        public int RequestsPerSecondPerIp { get; set; } = 50;

        /// <summary>Envios (POST) por minuto de cada IP em login, cadastro e recuperação de senha. Complementa o bloqueio da conta.</summary>
        public int AuthRequestsPerMinutePerIp { get; set; } = 20;
    }
}
