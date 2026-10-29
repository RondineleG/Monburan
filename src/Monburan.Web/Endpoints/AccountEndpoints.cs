using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Identity;

using Monburan.Core.DTOs.Request;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Conta do usuário autenticado pelo app. A exclusão segue o mesmo fluxo da página <c>/conta/excluir-dados</c>: confere a
/// senha, apaga o usuário do Identity e anonimiza os perfis (candidaturas ficam anônimas no histórico das empresas).
/// As lojas de aplicativo exigem que a exclusão seja possível de dentro do app.
/// </summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/account").WithTags("Account")
            .RequireAuthorization("ApiBearer");

        group.MapPost("/delete", DeleteAsync)
            .WithSummary("Exclui a conta autenticada e anonimiza os dados pessoais (exige a senha)");

        return group;
    }

    private static async Task<IResult> DeleteAsync(
        DeleteAccountRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AccountProfileService accountProfiles,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Monburan.Account");

        // "sub" do JWT; com o mapeamento padrão do JwtBearer ele chega como NameIdentifier
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão inválida");

        if (await userManager.HasPasswordAsync(user) && !await userManager.CheckPasswordAsync(user, request.Password ?? string.Empty))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["Senha incorreta."]
            }, title: "Erro de validação");
        }

        var email = user.Email;
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            logger.LogError("Falha ao excluir a conta {UserId} pelo app: {Errors}", userId,
                string.Join("; ", result.Errors.Select(e => e.Code)));
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível excluir sua conta agora. Tente novamente em instantes.");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                await accountProfiles.AnonymizeProfilesAsync(email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Conta {UserId} excluída pelo app, mas a anonimização do perfil falhou", userId);
            }
        }

        logger.LogInformation("Usuário {UserId} excluiu a própria conta pelo app.", userId);
        return Results.NoContent();
    }
}
