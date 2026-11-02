using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Identity;

using Monburan.Core.DTOs.Request;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

/// <summary>
/// Conta do usuário autenticado pelo app. A exclusão segue o mesmo fluxo da página <c>/conta/excluir-dados</c>: confere a
/// senha (com bloqueio por tentativas), anonimiza os perfis e só então apaga o usuário do Identity (candidaturas ficam anônimas no histórico das empresas).
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
        SignInManager<ApplicationUser> signInManager,
        AccountProfileService accountProfiles,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Monburan.Account");

        // "sub" do JWT; com o mapeamento padrão do JwtBearer ele chega como NameIdentifier
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
        if (user is null)
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão inválida");

        if (await userManager.HasPasswordAsync(user))
        {
            // lockoutOnFailure: a senha errada conta como tentativa e, no limite do Identity (5 erros, 15 minutos),
            // bloqueia a conta. CheckPasswordAsync não contava nada: um token roubado permitiria testar senhas à vontade
            // numa ação destrutiva e irreversível
            var check = await signInManager.CheckPasswordSignInAsync(user, request.Password ?? string.Empty, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                logger.LogWarning("Exclusão de conta pelo app recusada: {UserId} está bloqueado por tentativas de senha", userId);
                return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Muitas tentativas com senha incorreta. Tente de novo em alguns minutos.");
            }

            if (!check.Succeeded)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["Senha incorreta."]
                }, title: "Erro de validação");
            }
        }

        // Anonimiza o perfil ANTES de apagar o usuário. Na ordem inversa, uma falha na anonimização deixava nome, telefone
        // e currículos no banco sem dono (LGPD). Agora, se ela falhar, nada foi apagado e a pessoa pode tentar de novo.
        var email = user.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                await accountProfiles.AnonymizeProfilesAsync(email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao anonimizar o perfil da conta {UserId}; a exclusão pelo app foi interrompida", userId);
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                    title: "Não foi possível excluir sua conta agora. Tente novamente em instantes.");
            }
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            logger.LogError("Falha ao excluir a conta {UserId} pelo app: {Errors}", userId,
                string.Join("; ", result.Errors.Select(e => e.Code)));
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível excluir sua conta agora. Tente novamente em instantes.");
        }

        logger.LogInformation("Usuário {UserId} excluiu a própria conta pelo app.", userId);
        return Results.NoContent();
    }
}
