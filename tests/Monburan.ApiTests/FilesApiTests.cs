using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

public class FilesApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public FilesApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_ValidPdf_ReturnsFileUrl()
    {
        var token = await RegisterCandidateAsync();

        using var content = new MultipartFormDataContent();
        var pdf = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, .. "fake-pdf"u8]);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(pdf, "file", "curriculo.pdf");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/upload?fileType=Pdf")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        body!.FileUrl.Should().StartWith("/uploads/");
        body.OriginalFileName.Should().Be("curriculo.pdf");
    }

    [Fact]
    public async Task Upload_PdfTypeWithTextContent_Returns400()
    {
        var token = await RegisterCandidateAsync();

        using var content = new MultipartFormDataContent();
        var txt = new ByteArrayContent("texto puro"u8.ToArray());
        txt.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(txt, "file", "arquivo.txt");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/upload?fileType=Pdf")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OtherFileType_Returns400()
    {
        // fileType=Other pularia a validação de content-type — vetor de stored XSS
        var token = await RegisterCandidateAsync();

        using var content = new MultipartFormDataContent();
        var html = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        html.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        content.Add(html, "file", "evil.html");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/upload?fileType=Other")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ImageTypeWithHtmlFilename_StoresCanonicalExtension()
    {
        // Filename .html com content-type image/png deve ser salvo como .png
        var token = await RegisterCandidateAsync();

        using var content = new MultipartFormDataContent();
        var png = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, .. "fake-png"u8]);
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(png, "file", "evil.html");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/upload?fileType=Image")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        body!.FileUrl.Should().EndWith(".png");
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2, 3]), "file", "a.pdf");

        var response = await _client.PostAsync("/api/v1/files/upload?fileType=Pdf", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<UserLoginResponse> RegisterCandidateAsync()
    {
        var email = $"upload.{Guid.NewGuid():N}@teste.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Upload Teste",
            Email = email,
            Password = "Monburan123",
            ConfirmPassword = "Monburan123"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!;
    }
}
