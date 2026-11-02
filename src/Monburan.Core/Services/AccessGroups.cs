using System.Security.Claims;
using System.Text.RegularExpressions;

using Monburan.Core.Entities;

namespace Monburan.Core.Services;

/// <summary>
/// Grupos de acesso usados pelas feature flags. Uma conta pertence aos grupos implícitos que vêm do papel e do perfil
/// (admin, produto, candidato, recrutador) e aos grupos atribuídos pela claim <see cref="ClaimType"/>.
/// </summary>
public static partial class AccessGroups
{
    /// <summary>Claim do Identity que liga a conta a um grupo atribuído (valor = nome do grupo).</summary>
    public const string ClaimType = "monburan:grupo";

    public const string Admin = "admin";
    public const string Product = "produto";
    public const string Candidate = "candidato";
    public const string Recruiter = "recrutador";

    /// <summary>Papel do Identity de quem cuida das funcionalidades sem ser administrador.</summary>
    public const string ProductRole = "Produto";

    public static IReadOnlyList<(string Name, string Description)> Implicit { get; } =
    [
        (Admin, "Contas com papel de administrador"),
        (Product, "Contas com papel de produto"),
        (Candidate, "Todas as contas de candidato"),
        (Recruiter, "Todas as contas de empresa (recrutadores)")
    ];

    public const int MaxNameLength = 40;

    public static bool IsImplicit(string name) => Implicit.Any(group => group.Name == name);

    /// <summary>Nome válido: minúsculas, números e hífen (ex.: "recrutador-beta").</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength && NamePattern().IsMatch(name);

    /// <summary>Grupos implícitos da conta, a partir do papel e da claim de perfil (user_type).</summary>
    public static IEnumerable<string> ImplicitOf(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            yield break;

        if (user.IsInRole("Admin"))
            yield return Admin;
        if (user.IsInRole(ProductRole))
            yield return Product;

        var userType = user.FindFirst("user_type")?.Value;
        if (userType == "candidate")
            yield return Candidate;
        if (userType == "advertiser")
            yield return Recruiter;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex NamePattern();
}

/// <summary>Regra única de avaliação: liberada para todos, ou a conta está em pelo menos um dos grupos da flag.</summary>
public static class FeatureFlagRules
{
    public static bool IsEnabled(FeatureFlag flag, IReadOnlySet<string> userGroups) =>
        flag.EnabledForEveryone || flag.GroupList.Any(userGroups.Contains);
}
