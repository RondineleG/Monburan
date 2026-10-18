using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

public class JobsApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly HttpClient _client;

    public JobsApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListJobs_Anonymous_ReturnsPagedJobs()
    {
        var response = await _client.GetAsync("/api/v1/jobs?page=1&size=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ListJobsResponse>();
        body.Should().NotBeNull();
        body!.Jobs.Should().NotBeNull();
        body.Page.Should().Be(1);
        body.Size.Should().Be(5);
    }

    [Fact]
    public async Task GetJob_WithSeededJob_ReturnsDetails()
    {
        var list = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?page=1&size=1");
        list.Should().NotBeNull();
        var job = list!.Jobs.FirstOrDefault();
        job.Should().NotBeNull("o seed de desenvolvimento deve criar vagas");

        var response = await _client.GetAsync($"/api/v1/jobs/{job!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<JobResponse>();
        detail!.Id.Should().Be(job.Id);
        detail.Title.Should().Be(job.Title);
    }

    [Fact]
    public async Task GetJob_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListJobs_WithSearchTerm_FiltersResults()
    {
        var response = await _client.GetAsync("/api/v1/jobs?searchTerm=termo-que-nao-existe-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListJobsResponse>();
        body!.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task ListJobs_Pagination_ReturnsFilteredTotalNotPageSize()
    {
        var response = await _client.GetAsync("/api/v1/jobs?page=1&size=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListJobsResponse>();

        // TotalCount deve ser o total filtrado, não o número de itens da página
        body!.TotalCount.Should().BeGreaterThan(1, "o seed cria várias vagas — size=1 deve expor HasNextPage");
        body.HasNextPage.Should().BeTrue();
        body.TotalPages.Should().Be(body.TotalCount);
    }

    [Fact]
    public async Task ListJobs_SortByTitle_OrdersAlphabetically()
    {
        var response = await _client.GetAsync("/api/v1/jobs?page=1&size=20&sortBy=title&sortDescending=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListJobsResponse>();
        var titles = body!.Jobs.Select(j => j.Title).ToList();
        titles.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ListJobs_SortBySalary_ListsHigherSalariesFirst_AndJobsWithoutSalaryLast()
    {
        var response = await _client.GetAsync("/api/v1/jobs?page=1&size=100&sortBy=salary&sortDescending=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListJobsResponse>();
        var jobs = body!.Jobs.ToList();

        var withSalary = jobs.TakeWhile(j => j.SalaryMax.HasValue).ToList();
        withSalary.Should().NotBeEmpty("o seed cria vagas com faixa salarial");
        withSalary.Select(j => j.SalaryMax!.Value).Should().BeInDescendingOrder();

        // Depois da primeira vaga sem faixa salarial não pode aparecer nenhuma com faixa (no PostgreSQL o NULL
        // vem primeiro em ordem decrescente, então a consulta ordena explicitamente)
        jobs.Skip(withSalary.Count).Should().OnlyContain(j => !j.SalaryMax.HasValue);
    }
}
