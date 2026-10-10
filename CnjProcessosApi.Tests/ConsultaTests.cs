using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CnjProcessosApi.Core.Models;
using CnjProcessosApi.Infrastructure.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CnjProcessosApi.Tests;

public class ConsultaTests
{
    [Fact]
    public async Task ChaveOpcionalEUsadaSomenteNaRequisicao()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Payload);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api-publica.datajud.cnj.jus.br/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("APIKey", "default-key");
        var client = new CnjClient(http, NullLogger<CnjClient>.Instance);
        await client.BuscarProcessoAsync(Numero, "api_publica_trf1/_search", apiKey: "APIKey visitor-key");
        Assert.Equal("visitor-key", handler.Key);
        Assert.Equal("default-key", http.DefaultRequestHeaders.Authorization.Parameter);
        await client.BuscarProcessoAsync(Numero, "api_publica_trf1/_search");
        Assert.Equal("default-key", handler.Key);
    }

    [Fact]
    public async Task ChaveDoVisitanteNaoPodeSerEnviadaAOutroHost()
    {
        var client = CreateClient(new(HttpStatusCode.OK, Payload));
        await Assert.ThrowsAsync<DataJudException>(() => client.BuscarProcessoAsync(Numero, "api_publica_trf1/_search", apiKey: "visitor-key"));
    }
    public const string Numero = "00008323520184013202";

    [Theory]
    [InlineData("0000832-35.2018.4.01.3202")]
    [InlineData(Numero)]
    public void NormalizaEIdentificaTribunal(string valor)
    {
        Assert.Equal(Numero, NumeroCnj.Normalizar(valor));
        Assert.Equal("api_publica_trf1/_search", NumeroCnj.Tribunal(NumeroCnj.Normalizar(valor)));
    }

    [Theory]
    [InlineData("0000832-34.2018.4.01.3202")]
    [InlineData("abc00008323520184013202")]
    [InlineData("")]
    public void RejeitaNumeroInvalido(string numero) => Assert.Throws<ArgumentException>(() => NumeroCnj.Normalizar(numero));

    [Fact]
    public async Task LeRespostaRealOrdenaMovimentosEEnviaNumeroSemMascara()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Payload);
        var client = CreateClient(handler);
        var (processo, sigilo) = await client.BuscarProcessoAsync("0000832-35.2018.4.01.3202", "api_publica_trf1/_search");
        Assert.False(sigilo);
        Assert.NotNull(processo);
        Assert.Equal("Procedimento", processo.Classe);
        Assert.Equal("Concessão; Benefício", processo.Assunto);
        Assert.Equal("JEF", processo.OrgaoJulgador);
        Assert.Equal("Sentença", processo.Movimentacoes.First().Descricao);
        Assert.Equal(2, processo.Movimentacoes.Count);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal(Numero, request.RootElement.GetProperty("query").GetProperty("match").GetProperty("numeroProcesso").GetString());
    }

    [Fact]
    public async Task RespostaVaziaNaoCriaProcesso()
    {
        var result = await CreateClient(new(HttpStatusCode.OK, """{"hits":{"hits":[]}}""")).BuscarProcessoAsync(Numero, "api_publica_trf1/_search");
        Assert.Null(result.processo);
        Assert.False(result.sigiloso);
    }

    [Theory]
    [InlineData(401, 503)]
    [InlineData(403, 503)]
    [InlineData(404, 502)]
    [InlineData(429, 503)]
    [InlineData(500, 502)]
    public async Task ErroExternoNaoViraProcessoAusenteOuSigiloso(int status, int expected)
    {
        var ex = await Assert.ThrowsAsync<DataJudException>(() => CreateClient(new((HttpStatusCode)status, "{}"))
            .BuscarProcessoAsync(Numero, "api_publica_trf1/_search"));
        Assert.Equal(expected, ex.StatusCode);
    }

    [Fact]
    public async Task SigiloNaoRetornaMetadados()
    {
        var result = await CreateClient(new(HttpStatusCode.OK, Payload.Replace("\"nivelSigilo\":0", "\"nivelSigilo\":1")))
            .BuscarProcessoAsync(Numero, "api_publica_trf1/_search");
        Assert.True(result.sigiloso);
        Assert.Null(result.processo);
    }

    [Fact]
    public async Task CancelamentoNaoEViradoEmSucesso()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateClient(new(HttpStatusCode.OK, Payload))
            .BuscarProcessoAsync(Numero, "api_publica_trf1/_search", source.Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"timed_out\":true}")]
    public async Task RespostaInvalidaOuParcialFalha(string json)
    {
        await Assert.ThrowsAsync<DataJudException>(() => CreateClient(new(HttpStatusCode.OK, json))
            .BuscarProcessoAsync(Numero, "api_publica_trf1/_search"));
    }

    private static CnjClient CreateClient(StubHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("APIKey", "test");
        return new(http, NullLogger<CnjClient>.Instance);
    }

    public const string Payload = """
    {"hits":{"hits":[{"_source":{
      "numeroProcesso":"00008323520184013202","nivelSigilo":0,
      "classe":{"nome":"Procedimento"},"orgaoJulgador":{"nome":"JEF"},
      "assuntos":[[{"nome":"Concessão"}],{"nome":"Benefício"}],
      "dataHoraUltimaAtualizacao":"2025-01-02T10:00:00Z",
      "movimentos":[{"nome":"Distribuição","dataHora":"2024-01-01T10:00:00Z"},
                    {"nome":"Sentença","dataHora":"2025-01-01T10:00:00Z"}]
    }}]}}
    """;
}

public class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public string? Key { get; private set; }
    public string? Body { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Key = request.Headers.Authorization?.Parameter;
        Body = await request.Content!.ReadAsStringAsync(ct);
        return new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}
