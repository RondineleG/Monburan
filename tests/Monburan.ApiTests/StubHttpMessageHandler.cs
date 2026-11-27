using System.Net;
using System.Text;

namespace Monburan.ApiTests;

/// <summary>Handler em fila para o HttpClient tipado: cada resposta é consumida na ordem em que foi enfileirada.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _responses.Enqueue(respond);

    public void EnqueueJson(HttpStatusCode status, string body) =>
        Enqueue(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));

    public void EnqueueStatus(HttpStatusCode status) =>
        Enqueue(_ => Task.FromResult(new HttpResponseMessage(status)));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return _responses.Count == 0
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway))
            : _responses.Dequeue()(request);
    }
}
