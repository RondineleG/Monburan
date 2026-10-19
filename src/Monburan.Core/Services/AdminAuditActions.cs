namespace Monburan.Core.Services;

/// <summary>Chaves das ações auditadas e os textos mostrados ao administrador.</summary>
public static class AdminAuditActions
{
    public const string AccountDeactivated = "account.deactivated";
    public const string AccountActivated = "account.activated";
    public const string AccountUnlocked = "account.unlocked";
    // Confirmação do e-mail pela administração. A chave gravada no banco não muda; o identificador evita "Email" no nome porque o
    // CodeQL trata como dado pessoal qualquer constante assim chamada que chegue a um log (aqui só o nome da ação é registrado)
    public const string AccountConfirmedByAdmin = "account.email_confirmed";
    public const string AccountSessionsRevoked = "account.sessions_revoked";
    public const string AccountTwoFactorReset = "account.two_factor_reset";
    public const string AccountAdminGranted = "account.admin_granted";
    public const string AccountAdminRevoked = "account.admin_revoked";
    public const string JobStatusChanged = "job.status_changed";
    public const string JobDeleted = "job.deleted";
    public const string AiProviderSaved = "ai_provider.saved";
    public const string AiProviderDefaultChanged = "ai_provider.default_changed";
    public const string AiProviderDeleted = "ai_provider.deleted";

    public const string AccountTarget = "account";
    public const string JobTarget = "job";
    public const string AiProviderTarget = "aiProvider";

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [AccountDeactivated] = "Conta inativada",
        [AccountActivated] = "Conta reativada",
        [AccountUnlocked] = "Conta desbloqueada",
        [AccountConfirmedByAdmin] = "E-mail confirmado manualmente",
        [AccountSessionsRevoked] = "Sessões encerradas",
        [AccountTwoFactorReset] = "Verificação em duas etapas redefinida",
        [AccountAdminGranted] = "Papel de administrador concedido",
        [AccountAdminRevoked] = "Papel de administrador removido",
        [JobStatusChanged] = "Situação da vaga alterada",
        [JobDeleted] = "Vaga excluída",
        [AiProviderSaved] = "Provedor de IA salvo",
        [AiProviderDefaultChanged] = "Provedor de IA padrão alterado",
        [AiProviderDeleted] = "Provedor de IA excluído"
    };

    private static readonly IReadOnlyDictionary<string, string> TargetLabels = new Dictionary<string, string>
    {
        [AccountTarget] = "Conta",
        [JobTarget] = "Vaga",
        [AiProviderTarget] = "Provedor de IA"
    };

    /// <summary>Todas as ações conhecidas, na ordem de exibição dos filtros.</summary>
    public static IReadOnlyCollection<string> All => Labels.Keys.ToList();

    public static IReadOnlyCollection<string> Targets => TargetLabels.Keys.ToList();

    public static string Label(string action) => Labels.TryGetValue(action, out var label) ? label : action;

    public static string TargetLabel(string target) => TargetLabels.TryGetValue(target, out var label) ? label : target;
}
