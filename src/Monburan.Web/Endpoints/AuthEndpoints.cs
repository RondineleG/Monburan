using System.ComponentModel.DataAnnotations;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Advertisers;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.Validations;
using Monburan.Web.Data;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group.MapPost("/login", async (
            UserLoginRequest request,
            IIdentityService identityService,
            CancellationToken cancellationToken) =>
        {
            var response = await identityService.Login(request);
            return response.Sucesso
                ? Results.Ok(response)
                : Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Falha na autenticação",
                    detail: string.Join("; ", response.Erros));
        })
        .WithSummary("Autentica usuário e retorna access token + refresh token JWT");

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IIdentityService identityService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["refreshToken"] = ["Refresh token é obrigatório"]
                }, title: "Erro de validação");

            var response = await identityService.RefreshToken(request.RefreshToken);
            return response.Sucesso
                ? Results.Ok(response)
                : Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Refresh token inválido",
                    detail: string.Join("; ", response.Erros));
        })
        .WithSummary("Renova o par de tokens a partir de um refresh token válido");

        group.MapPost("/register", Register)
            .WithSummary("Cria conta (Identity) + perfil de candidato ou anunciante, retorna tokens");

        group.MapPost("/forgot-password", ForgotPassword)
            .WithSummary("Envia e-mail com link de redefinição de senha (deep-link para o site)");

        return group;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        CreateCandidateUseCase createCandidate,
        CreateAdvertiserUseCase createAdvertiser,
        IIdentityService identityService,
        EmailConfirmationUseCase emailConfirmationUseCase,
        ApplicationDbContext dbContext,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Monburan.Api.Auth");

        // Minimal APIs não executam DataAnnotations — validação manual é obrigatória
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (request.AccountType is not ("candidate" or "advertiser"))
            errors["accountType"] = ["Escolha 'candidate' ou 'advertiser'."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 100)
            errors["name"] = ["O nome deve ter entre 2 e 100 caracteres."];
        if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email))
            errors["email"] = ["Informe um e-mail válido."];
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < PasswordPolicy.MinLength || request.Password.Length > PasswordPolicy.MaxLength)
            errors["password"] = [$"A senha deve ter entre {PasswordPolicy.MinLength} e {PasswordPolicy.MaxLength} caracteres."];
        if (request.ConfirmPassword != request.Password)
            errors["confirmPassword"] = ["As senhas não conferem."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors, title: "Erro de validação");

        var user = new ApplicationUser
        {
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        var nameParts = request.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        user.FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
        user.LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        await userStore.SetUserNameAsync(user, request.Email.Trim(), cancellationToken);
        var emailStore = (IUserEmailStore<ApplicationUser>)userStore;
        await emailStore.SetEmailAsync(user, request.Email.Trim(), cancellationToken);

        // A conta e o perfil entram na mesma transação: o perfil é gravado na mesma unidade
        // de trabalho da conta (o mesmo DbContext do escopo), então uma falha no fim não
        // deixa um perfil órfão sem conta nem uma conta sem perfil.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var identityResult = await userManager.CreateAsync(user, request.Password);
            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [string.Empty] = identityResult.Errors.Select(e => e.Description).ToArray()
                }, title: "Erro de validação");
            }

            // Propaga o status real do UseCase — 409 para e-mail duplicado,
            // 400/ValidationProblem para dados inválidos, em vez de achar tudo em 409
            RequestResult profileResult = request.AccountType == "candidate"
                ? await createCandidate.ExecuteAsync(new CreateCandidateRequest
                {
                    Name = request.Name.Trim(),
                    Email = request.Email.Trim()
                }, cancellationToken)
                : await createAdvertiser.ExecuteAsync(new CreateAdvertiserRequest
                {
                    Name = request.Name.Trim(),
                    Email = request.Email.Trim(),
                    PersonType = EPersonType.Company
                }, cancellationToken);

            if (profileResult.Status != EResultStatus.Success)
            {
                await transaction.RollbackAsync(cancellationToken);
                return profileResult.ToHttpResult();
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "E-mail já cadastrado",
                detail: "Já existe um perfil associado a este e-mail.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Falha ao criar perfil {AccountType} para {Email}; cadastro desfeito",
                request.AccountType, request.Email);
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível concluir o cadastro");
        }

        logger.LogInformation("API: nova conta {AccountType} criada para {Email}", request.AccountType, request.Email);

        // E-mail de confirmação — mesmo fluxo da página Register.razor (best effort).
        // Um único envio: SendConfirmationLinkAsync do Identity + EmailConfirmationUseCase
        // mandariam dois e-mails para o mesmo usuário.
        try
        {
            var userId = await userManager.GetUserIdAsync(user);
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = $"{PublicBaseUri(httpContext, configuration)}/Account/ConfirmEmail?userId={Uri.EscapeDataString(userId)}&code={Uri.EscapeDataString(code)}";

            await emailConfirmationUseCase.ExecuteAsync(new EmailConfirmationRequest
            {
                UserId = userId,
                Email = request.Email,
                UserName = request.Email.Split('@').FirstOrDefault() ?? request.Email,
                ConfirmationLink = callbackUrl
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar e-mail de confirmação para {Email}", request.Email);
        }

        // Auto-login: emite tokens imediatamente após o registro
        var login = await identityService.Login(new UserLoginRequest
        {
            Email = request.Email,
            Password = request.Password
        });

        if (!login.Sucesso)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Conta criada, mas o login automático falhou",
                detail: "Tente entrar com e-mail e senha.");
        }

        return Results.Json(login, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        PasswordResetUseCase passwordResetUseCase,
        HttpContext httpContext,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // Resposta sempre 200 — não vaza se o e-mail existe
        if (string.IsNullOrWhiteSpace(request.Email)
            || !new EmailAddressAttribute().IsValid(request.Email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["Informe um e-mail válido."]
            }, title: "Erro de validação");
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Results.Ok();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetLink = $"{PublicBaseUri(httpContext, configuration)}/Account/ResetPassword?code={Uri.EscapeDataString(encoded)}";

        await passwordResetUseCase.ExecuteAsync(new PasswordResetRequest
        {
            Email = request.Email,
            ResetLink = resetLink
        }, cancellationToken);

        return Results.Ok();
    }

    // Links de e-mail precisam da URL pública — atrás de proxy TLS sem
    // ForwardedHeaders, Request.Scheme/Host gerariam links http://internos
    private static string PublicBaseUri(HttpContext httpContext, IConfiguration configuration)
    {
        var configured = configuration["App:PublicBaseUrl"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured.TrimEnd('/')
            : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    }
}
