using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.Mobile.Shared.Api;

/// <summary>
/// Cliente tipado da API Monburan v1 (endpoints autenticados/anônimos).
/// Lança <see cref="ApiException"/> em erros HTTP e em falhas de rede
/// (IsNetworkError = offline).
/// </summary>
public class MonburanApiClient
{
    private readonly HttpClient _http;

    public MonburanApiClient(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient(ApiClientNames.Api);
    }

    // ---- Jobs (público) ----

    public Task<ListJobsResponse> GetJobsAsync(ListJobsRequest request, CancellationToken ct = default) =>
        SendAsync<ListJobsResponse>(HttpMethod.Get, $"api/v1/jobs{ToQueryString(request)}", null, ct);

    public Task<JobResponse> GetJobAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<JobResponse>(HttpMethod.Get, $"api/v1/jobs/{id}", null, ct);

    // ---- Candidates ----

    public Task<CandidateResponse> GetMyCandidateAsync(CancellationToken ct = default) =>
        SendAsync<CandidateResponse>(HttpMethod.Get, "api/v1/candidates/me", null, ct);

    public Task<CandidateResponse> UpdateMyCandidateAsync(UpdateCandidateRequest request, CancellationToken ct = default) =>
        SendAsync<CandidateResponse>(HttpMethod.Put, "api/v1/candidates/me", request, ct);

    // ---- Applications ----

    public Task<ApplicationResponse> ApplyAsync(Guid jobId, string? notes = null, Guid? resumeId = null, CancellationToken ct = default) =>
        SendAsync<ApplicationResponse>(HttpMethod.Post, "api/v1/applications",
            new CreateApplicationRequest { JobId = jobId, Notes = notes, ResumeId = resumeId }, ct);

    public Task<List<ApplicationResponse>> GetMyApplicationsAsync(CancellationToken ct = default) =>
        SendAsync<List<ApplicationResponse>>(HttpMethod.Get, "api/v1/applications/mine", null, ct);

    // ---- Resumes ----

    public Task<List<ResumeResponse>> GetMyResumesAsync(CancellationToken ct = default) =>
        SendAsync<List<ResumeResponse>>(HttpMethod.Get, "api/v1/resumes/mine", null, ct);

    public Task<ResumeResponse> GetResumeAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ResumeResponse>(HttpMethod.Get, $"api/v1/resumes/{id}", null, ct);

    public Task<ResumeResponse> CreateResumeAsync(CreateResumeRequest request, CancellationToken ct = default) =>
        SendAsync<ResumeResponse>(HttpMethod.Post, "api/v1/resumes", request, ct);

    public Task<ResumeResponse> UpdateResumeAsync(Guid id, UpdateResumeRequest request, CancellationToken ct = default) =>
        SendAsync<ResumeResponse>(HttpMethod.Put, $"api/v1/resumes/{id}", request, ct);

    public Task DeleteResumeAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/v1/resumes/{id}", null, ct);

    // ---- Files ----

    public Task<UploadFileResponse> UploadPdfAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var file = new StreamContent(stream);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        var content = new MultipartFormDataContent { { file, "file", fileName } };
        return SendContentAsync<UploadFileResponse>(HttpMethod.Post, "api/v1/files/upload?fileType=Pdf", content, ct);
    }

    // ---- Notifications ----

    public Task<List<NotificationResponse>> GetMyNotificationsAsync(CancellationToken ct = default) =>
        SendAsync<List<NotificationResponse>>(HttpMethod.Get, "api/v1/notifications/mine", null, ct);

    public Task MarkNotificationAsReadAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Put, $"api/v1/notifications/{id}/read", null, ct);

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var result = await SendAsync<UnreadCountResponse>(HttpMethod.Get, "api/v1/notifications/mine/unread-count", null, ct);
        return result.Count;
    }

    private sealed class UnreadCountResponse
    {
        public int Count { get; set; }
    }

    // ---- Plans ----

    public Task<AvailablePlansResponse> GetPlansAsync(CancellationToken ct = default) =>
        SendAsync<AvailablePlansResponse>(HttpMethod.Get, "api/v1/plans", null, ct);

    // ---- infra ----

    internal static async Task<T> SendAsync<T>(HttpClient client, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: ApiJson.Options);

        using var response = await SendRequestAsync(client, request, ct);
        return await ReadBodyAsync<T>(response, ct);
    }

    private Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct) =>
        SendAsync<T>(_http, method, path, body, ct);

    private async Task<T> SendContentAsync<T>(HttpMethod method, string path, HttpContent content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        using var response = await SendRequestAsync(_http, request, ct);
        return await ReadBodyAsync<T>(response, ct);
    }

    internal static async Task<HttpResponseMessage> SendRequestAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw ApiException.Network();
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw ApiException.Network("O servidor demorou para responder. Tente novamente.");
        }

        if (!response.IsSuccessStatusCode)
            throw await ToApiExceptionAsync(response, ct);

        return response;
    }

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
            return default!;

        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, ct))!;
    }

    private static async Task<ApiException> ToApiExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sessão expirada. Entre novamente.",
            HttpStatusCode.Forbidden => "Você não tem permissão para esta ação.",
            HttpStatusCode.NotFound => "Recurso não encontrado.",
            HttpStatusCode.Conflict => "Conflito: o recurso já existe ou já foi processado.",
            >= HttpStatusCode.InternalServerError => "Erro no servidor. Tente novamente em instantes.",
            _ => "Não foi possível concluir a operação."
        };

        Dictionary<string, string[]>? validationErrors = null;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(ApiJson.Options, ct);
            if (problem is not null)
            {
                // Detail carrega a mensagem de domínio ("Vaga não está mais ativa" etc.);
                // Title é o rótulo genérico do status
                if (!string.IsNullOrWhiteSpace(problem.Detail))
                    message = problem.Detail;
                else if (!string.IsNullOrWhiteSpace(problem.Title))
                    message = problem.Title;

                if (problem.Errors is { Count: > 0 })
                    validationErrors = problem.Errors;
            }
        }
        catch (JsonException)
        {
            // Resposta sem body JSON — usa a mensagem padrão
        }

        return new ApiException(response.StatusCode, message, validationErrors);
    }

    private static string ToQueryString(ListJobsRequest r)
    {
        var query = new List<string> { $"page={r.Page}", $"size={r.Size}" };

        if (!string.IsNullOrWhiteSpace(r.SearchTerm)) query.Add($"searchTerm={Uri.EscapeDataString(r.SearchTerm)}");
        if (!string.IsNullOrWhiteSpace(r.City)) query.Add($"city={Uri.EscapeDataString(r.City)}");
        if (!string.IsNullOrWhiteSpace(r.State)) query.Add($"state={Uri.EscapeDataString(r.State)}");
        if (r.IsRemote.HasValue) query.Add($"isRemote={r.IsRemote.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(r.Type)) query.Add($"type={Uri.EscapeDataString(r.Type)}");
        if (r.SalaryMin.HasValue)
            query.Add(FormattableString.Invariant($"salaryMin={r.SalaryMin.Value}"));
        if (r.SalaryMax.HasValue)
            query.Add(FormattableString.Invariant($"salaryMax={r.SalaryMax.Value}"));
        if (!string.IsNullOrWhiteSpace(r.Category)) query.Add($"category={Uri.EscapeDataString(r.Category)}");
        if (!string.IsNullOrWhiteSpace(r.SortBy)) query.Add($"sortBy={Uri.EscapeDataString(r.SortBy)}");
        query.Add($"sortDescending={r.SortDescending.ToString().ToLowerInvariant()}");
        if (r.AdvertiserId.HasValue) query.Add($"advertiserId={r.AdvertiserId.Value}");

        return "?" + string.Join("&", query);
    }

    private sealed class ApiProblemDetails
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }
    }
}
