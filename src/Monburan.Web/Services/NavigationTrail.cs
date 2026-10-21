namespace Monburan.Web.Services;

/// <summary>Papel de quem navega, para os rótulos da trilha e do menu.</summary>
public enum NavRole
{
    Visitor,
    Candidate,
    Advertiser,
    Admin
}

/// <summary>Um passo da trilha: com <see cref="Href"/> é link; o último passo é a página atual.</summary>
public sealed record TrailItem(string Label, string? Href = null);

/// <summary>
/// Trilha da barra superior ("Área › Página"): a mesma orientação em todas as telas, com os nomes do menu.
/// O título da página é o h1 de cada tela; a trilha só diz onde a pessoa está e como voltar um nível.
/// </summary>
public static class NavigationTrail
{
    public static IReadOnlyList<TrailItem> For(string path, NavRole role)
    {
        var p = "/" + (path ?? string.Empty).Split('?', '#')[0].Trim('/');
        var segments = p.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        TrailItem[] T(params TrailItem[] items) => items;
        var jobs = new TrailItem("Vagas", "/jobs");
        var resumes = new TrailItem("Meus currículos", "/resumes");
        var myJobs = new TrailItem("Minhas vagas", "/my-jobs");
        var received = new TrailItem("Candidaturas recebidas", "/received-applications");
        var account = new TrailItem("Conta e segurança", "/Account/Manage");
        var addresses = new TrailItem("Endereços", "/address/list");
        var companyProfile = new TrailItem("Perfil da empresa", "/profile/advertiser");

        return p.ToLowerInvariant() switch
        {
            "/" or "/dashboard" => T(new TrailItem(role is NavRole.Candidate or NavRole.Visitor ? "Início" : "Visão geral")),

            "/jobs" => T(new TrailItem("Vagas")),
            "/jobs/create" => T(myJobs, new TrailItem("Publicar vaga")),
            _ when Is(segments, "jobs", "edit", null) => T(myJobs, new TrailItem("Editar vaga")),
            _ when Is(segments, "jobs", null, "applications") => T(received, new TrailItem("Por vaga")),
            _ when Is(segments, "jobs", null) => T(jobs, new TrailItem("Vaga")),
            _ when Is(segments, "apply", null) => T(jobs, new TrailItem("Candidatura")),
            _ when Is(segments, "companies", null) => T(jobs, new TrailItem("Empresa")),

            "/my-applications" => T(new TrailItem("Minhas candidaturas")),
            "/career-profile" => T(new TrailItem("Perfil de carreira")),
            "/resumes" => T(new TrailItem("Meus currículos")),
            "/resumes/new" or "/resumes/wizard" => T(resumes, new TrailItem("Novo currículo")),
            "/resumes/upload" => T(resumes, new TrailItem("Importar PDF")),
            _ when Is(segments, "resumes", null, "edit") => T(resumes, new TrailItem("Editar currículo")),
            _ when Is(segments, "resumes", null, "wizard") => T(resumes, new TrailItem("Revisar currículo")),
            _ when Is(segments, "resumes", null, "cv") => T(resumes, new TrailItem("Ver CV")),
            _ when Is(segments, "resumes", null) => T(resumes, new TrailItem("Currículo")),

            "/my-jobs" => T(new TrailItem("Minhas vagas")),
            "/received-applications" => T(new TrailItem("Candidaturas recebidas")),
            "/my-subscription" => T(new TrailItem("Plano e assinatura")),
            "/profile/advertiser" => T(new TrailItem("Perfil da empresa")),
            "/advertiser/upload-logo" => T(companyProfile, new TrailItem("Logo")),
            "/profile/candidate" => T(new TrailItem("Meu perfil")),

            "/notifications" => T(new TrailItem("Notificações")),
            "/plans" => T(new TrailItem("Planos")),
            "/terms" => T(new TrailItem("Termos de uso")),
            "/privacy" => T(new TrailItem("Política de privacidade")),

            "/address/list" => T(new TrailItem("Endereços")),
            "/address/create" => T(addresses, new TrailItem("Novo endereço")),
            _ when Is(segments, "address", "edit", null) => T(addresses, new TrailItem("Editar endereço")),
            _ when Is(segments, "address", "delete", null) => T(addresses, new TrailItem("Remover endereço")),

            "/account/manage" => T(new TrailItem("Conta e segurança")),
            _ when p.StartsWith("/account/manage/", StringComparison.OrdinalIgnoreCase) => T(account, new(ManagePage(segments[^1]))),

            "/admin/accounts" => T(new TrailItem("Administração", "/"), new TrailItem("Contas")),
            "/admin/jobs" => T(new TrailItem("Administração", "/"), new TrailItem("Vagas")),
            "/admin/candidates" => T(new TrailItem("Administração", "/"), new TrailItem("Candidatos")),
            "/admin/advertisers" => T(new TrailItem("Administração", "/"), new TrailItem("Empresas")),
            "/admin/ai-providers" => T(new TrailItem("Administração", "/"), new TrailItem("Provedores de IA")),
            "/admin/audit" => T(new TrailItem("Administração", "/"), new TrailItem("Auditoria")),

            _ => T(new TrailItem("Monburan"))
        };
    }

    // Segmentos fixos comparados sem caixa; null aceita qualquer valor (um id)
    private static bool Is(string[] segments, params string?[] pattern) =>
        segments.Length == pattern.Length
        && pattern.Select((part, i) => part is null || string.Equals(part, segments[i], StringComparison.OrdinalIgnoreCase)).All(ok => ok);

    private static string ManagePage(string page) => page.ToLowerInvariant() switch
    {
        "email" => "E-mail",
        "changepassword" => "Alterar senha",
        "setpassword" => "Definir senha",
        "twofactorauthentication" => "Verificação em duas etapas",
        "enableauthenticator" => "Aplicativo autenticador",
        "resetauthenticator" => "Redefinir autenticador",
        "disable2fa" => "Desativar verificação",
        "generaterecoverycodes" => "Códigos de recuperação",
        "personaldata" => "Dados pessoais",
        "deletepersonaldata" => "Excluir conta",
        "externallogins" => "Contas conectadas",
        _ => "Configurações"
    };
}
