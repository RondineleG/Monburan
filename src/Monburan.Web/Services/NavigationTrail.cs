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
        var resumes = new TrailItem("Meus currículos", "/curriculos");
        var myJobs = new TrailItem("Minhas vagas", "/minhas-vagas");
        var received = new TrailItem("Candidaturas recebidas", "/candidaturas-recebidas");
        var account = new TrailItem("Conta e segurança", "/conta");
        var addresses = new TrailItem("Endereços", "/enderecos");
        var companyProfile = new TrailItem("Perfil da empresa", "/perfil/empresa");

        return p.ToLowerInvariant() switch
        {
            "/" or "/painel" => T(new TrailItem(role is NavRole.Candidate or NavRole.Visitor ? "Início" : "Visão geral")),

            "/jobs" => T(new TrailItem("Vagas")),
            "/vagas/nova" => T(myJobs, new TrailItem("Publicar vaga")),
            _ when Is(segments, "vagas", null, "editar") => T(myJobs, new TrailItem("Editar vaga")),
            _ when Is(segments, "vagas", null, "candidaturas") => T(received, new TrailItem("Por vaga")),
            _ when Is(segments, "jobs", null) => T(jobs, new TrailItem("Vaga")),
            _ when Is(segments, "candidatar", null) => T(jobs, new TrailItem("Candidatura")),
            _ when Is(segments, "empresas", null) => T(jobs, new TrailItem("Empresa")),

            "/minhas-candidaturas" => T(new TrailItem("Minhas candidaturas")),
            "/perfil-profissional" => T(new TrailItem("Perfil de carreira")),
            "/curriculos" => T(new TrailItem("Meus currículos")),
            "/curriculos/novo" or "/curriculos/assistente" => T(resumes, new TrailItem("Novo currículo")),
            "/curriculos/upload" => T(resumes, new TrailItem("Importar PDF")),
            _ when Is(segments, "curriculos", null, "editar") => T(resumes, new TrailItem("Editar currículo")),
            _ when Is(segments, "curriculos", null, "assistente") => T(resumes, new TrailItem("Revisar currículo")),
            _ when Is(segments, "curriculos", null, "cv") => T(resumes, new TrailItem("Ver CV")),
            _ when Is(segments, "curriculos", null) => T(resumes, new TrailItem("Currículo")),

            "/minhas-vagas" => T(new TrailItem("Minhas vagas")),
            "/candidaturas-recebidas" => T(new TrailItem("Candidaturas recebidas")),
            "/minha-assinatura" => T(new TrailItem("Plano e assinatura")),
            "/perfil/empresa" => T(new TrailItem("Perfil da empresa")),
            "/empresa/logo" => T(companyProfile, new TrailItem("Logo")),
            "/perfil/candidato" => T(new TrailItem("Meu perfil")),

            "/notifications" => T(new TrailItem("Notificações")),
            "/plans" => T(new TrailItem("Planos")),
            "/termos" => T(new TrailItem("Termos de uso")),
            "/privacidade" => T(new TrailItem("Política de privacidade")),

            "/enderecos" => T(new TrailItem("Endereços")),
            "/enderecos/novo" => T(addresses, new TrailItem("Novo endereço")),
            _ when Is(segments, "enderecos", null, "editar") => T(addresses, new TrailItem("Editar endereço")),
            _ when Is(segments, "enderecos", null, "excluir") => T(addresses, new TrailItem("Remover endereço")),

            "/conta" => T(new TrailItem("Conta e segurança")),
            _ when p.StartsWith("/conta/", StringComparison.OrdinalIgnoreCase) => T(account, new(ManagePage(segments[^1]))),

            "/admin/contas" => T(new TrailItem("Administração", "/"), new TrailItem("Contas")),
            "/admin/vagas" => T(new TrailItem("Administração", "/"), new TrailItem("Vagas")),
            "/admin/candidatos" => T(new TrailItem("Administração", "/"), new TrailItem("Candidatos")),
            "/admin/empresas" => T(new TrailItem("Administração", "/"), new TrailItem("Empresas")),
            "/admin/provedores-ia" => T(new TrailItem("Administração", "/"), new TrailItem("Provedores de IA")),
            "/admin/auditoria" => T(new TrailItem("Administração", "/"), new TrailItem("Auditoria")),

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
        "alterar-senha" => "Alterar senha",
        "definir-senha" => "Definir senha",
        "seguranca-2fa" => "Verificação em duas etapas",
        "ativar-autenticador" => "Aplicativo autenticador",
        "redefinir-autenticador" => "Redefinir autenticador",
        "desativar-2fa" => "Desativar verificação",
        "codigos-recuperacao" => "Códigos de recuperação",
        "dados-pessoais" => "Dados pessoais",
        "excluir-dados" => "Excluir conta",
        "logins-externos" => "Contas conectadas",
        _ => "Configurações"
    };
}
