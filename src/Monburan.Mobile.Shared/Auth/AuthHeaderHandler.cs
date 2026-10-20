using System.Net;
using System.Net.Http.Headers;

namespace Monburan.Mobile.Shared.Auth;

/// <summary>
/// Anexa o access token a cada request e, em caso de 401, tenta renovar
/// a sessão e repetir a chamada uma única vez.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly TokenService _tokens;

    public AuthHeaderHandler(TokenService tokens)
    {
        _tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Bufferiza o corpo antes do primeiro envio — se houver retry pós-refresh,
        // CloneAsync consegue ler os bytes de volta (uploads multipart inclusos)
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        await AttachTokenAsync(request);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var outcome = await _tokens.RefreshAsync(cancellationToken);
        if (outcome == RefreshOutcome.Rejected)
            return response;

        if (outcome == RefreshOutcome.Unavailable)
        {
            // Não deu para renovar agora: é falha temporária, não sessão inválida. Devolver o 401 aqui faria a tela
            // tratar como sessão expirada e apagar uma sessão que continua valendo
            response.Dispose();
            throw new HttpRequestException("Não foi possível renovar a sessão agora.");
        }

        // Repete a chamada uma vez com o novo token — precisa de conteúdo clonável
        response.Dispose();
        using var retry = await CloneAsync(request);
        await AttachTokenAsync(retry);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            var content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        return clone;
    }
}
