using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CnjProcessosApi.Tests;

public class ApiTests
{
    [Fact]
    public async Task ChaveDoVisitanteNaoUsaNemAlteraCacheCompartilhado()
    {
        using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        var response = await http.PostAsJsonAsync("/api/processos/consulta", new { numeroCNJ = ConsultaTests.Numero, apiKey = "visitor-key" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("visitor-key", factory.Cnj.LastKey);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var list = await http.GetFromJsonAsync<JsonElement>("/api/processos");
        Assert.Equal(0, list.GetProperty("total").GetInt32());
        await http.GetAsync("/api/processos/" + ConsultaTests.Numero);
        Assert.Null(factory.Cnj.LastKey);
    }

    [Fact]
    public async Task ConsultaPostSemChaveUsaConsultaPublica()
    {
        using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/processos/consulta", new { numeroCNJ = ConsultaTests.Numero })).StatusCode);
        Assert.Null(factory.Cnj.LastKey);
    }
    [Fact]
    public async Task ConsultaPersisteCacheListaEReconsultaSemDuplicarMovimentos()
    {
        using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        var first = await http.GetAsync("/api/processos/" + ConsultaTests.Numero);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Sentença", body.GetProperty("ultimaMovimentacao").GetProperty("descricao").GetString());
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/processos/" + ConsultaTests.Numero)).StatusCode);
        var list = await http.GetFromJsonAsync<JsonElement>("/api/processos");
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(1, list.GetProperty("items")[0].GetProperty("movimentacoes").GetArrayLength());
        factory.Cnj.Restricted = true;
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/processos/" + ConsultaTests.Numero)).StatusCode);
        list = await http.GetFromJsonAsync<JsonElement>("/api/processos");
        Assert.Equal(0, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ValidaEntradasEEntregaPaginaSwaggerHealth()
    {
        using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        foreach (var url in new[] { "/api/processos/abc", "/api/processos?page=0", "/api/processos?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(url)).StatusCode);
        Assert.Contains("Consulte um processo judicial", await http.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/processos/sync", null)).StatusCode);
    }

    [Fact]
    public async Task LimiteRetorna429()
    {
        using var factory = new ApiFactory(2);
        using var http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/processos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/processos")).StatusCode);
        var response = await http.GetAsync("/api/processos");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task FalhaDoDataJudRetornaProblemaSemFalso404()
    {
        using var factory = new ApiFactory();
        factory.Cnj.Fail = true;
        using var http = factory.CreateClient();
        var response = await http.GetAsync("/api/processos/" + ConsultaTests.Numero);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("indisponível", await response.Content.ReadAsStringAsync());
    }
}

public class ApiFactory(int limit = 120) : WebApplicationFactory<Program>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"cnj-test-{Guid.NewGuid():N}.db");
    public FakeCnj Cnj { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={_db};Pooling=False",
            ["DataJud:CacheMinutes"] = "0",
            ["RateLimiting:PermitLimitPerMinute"] = limit.ToString(),
            ["Swagger:Enabled"] = "true",
            ["Processos:EnableManualSync"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICnjClient>();
            services.AddSingleton<ICnjClient>(Cnj);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-shm", "-wal" })
            if (File.Exists(_db + suffix)) File.Delete(_db + suffix);
    }
}

public class FakeCnj : ICnjClient
{
    public string? LastKey { get; private set; }
    public bool Restricted { get; set; }
    public bool Fail { get; set; }
    public Task<(Processo? processo, bool sigiloso)> BuscarProcessoAsync(string numero, string endpoint, CancellationToken ct = default, string? apiKey = null)
    {
        LastKey = apiKey;
        if (Fail) throw new DataJudException("DataJud indisponível", 503);
        return Task.FromResult<(Processo?, bool)>(Restricted ? (null, true) : (new Processo
        {
            NumeroCNJ = numero, Classe = "Procedimento", Movimentacoes = new List<Movimentacao>
            {
                new() { Data = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc), Descricao = "Sentença" }
            }
        }, false));
    }
}
