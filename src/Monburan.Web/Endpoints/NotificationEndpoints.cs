using Microsoft.EntityFrameworkCore;

using Monburan.Core.Enums;
using Monburan.Core.UseCases.Notifications;
using Monburan.Web.Services;

namespace Monburan.Web.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/notifications").WithTags("Notifications")
            .RequireAuthorization("ApiBearer");

        // RecipientId das notificações é o id do perfil (candidate/advertiser), igual ao site
        group.MapGet("/mine", async (
            ICurrentUserHelper currentUser,
            ListNotificationsByUserUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var recipientId = GetRecipientId(currentUser);
            if (recipientId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil necessário");

            var result = await useCase.ExecuteAsync(recipientId);
            return result.ToHttpResult();
        })
        .WithSummary("Lista notificações do usuário autenticado");

        // Badge do app: COUNT direto no banco, sem materializar a lista inteira
        group.MapGet("/mine/unread-count", async (
            ICurrentUserHelper currentUser,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            CancellationToken cancellationToken) =>
        {
            var recipientId = GetRecipientId(currentUser);
            if (recipientId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil necessário");

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var count = await context.Notifications
                .CountAsync(n => n.RecipientId == recipientId && !n.IsRead && !n.IsDeleted, cancellationToken);

            return Results.Ok(new { count });
        })
        .WithSummary("Contagem de notificações não lidas (badge do app)");

        group.MapPut("/mine/read-all", async (
            ICurrentUserHelper currentUser,
            MarkAllNotificationsAsReadUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var recipientId = GetRecipientId(currentUser);
            if (recipientId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil necessário");

            var result = await useCase.ExecuteAsync(recipientId);
            return result.Status == EResultStatus.Success
                ? Results.Ok(new { updated = result.Data })
                : result.ToHttpResult();
        })
        .WithSummary("Marca todas as notificações do usuário autenticado como lidas");

        group.MapPut("/{id:guid}/read", async (
            Guid id,
            ICurrentUserHelper currentUser,
            ListNotificationsByUserUseCase listUseCase,
            MarkNotificationAsReadUseCase useCase,
            CancellationToken cancellationToken) =>
        {
            var recipientId = GetRecipientId(currentUser);
            if (recipientId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Perfil necessário");

            // Checagem de propriedade fail-closed: se a listagem falhar, não marca nada
            var mine = await listUseCase.ExecuteAsync(recipientId);
            if (mine.Status != EResultStatus.Success || mine.Data is null)
                return mine.ToHttpResult();

            if (mine.Data.All(n => n.Id != id))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Notificação não encontrada");
            }

            var result = await useCase.ExecuteAsync(id);
            return result.ToHttpResult();
        })
        .WithSummary("Marca notificação como lida");

        return api;
    }

    private static string? GetRecipientId(ICurrentUserHelper currentUser)
    {
        return currentUser.GetUserType() switch
        {
            "candidate" => currentUser.GetCandidateId()?.ToString(),
            "advertiser" => currentUser.GetAdvertiserId()?.ToString(),
            _ => null,
        };
    }
}
