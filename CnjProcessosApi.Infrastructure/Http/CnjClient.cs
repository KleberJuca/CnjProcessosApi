using System.Net;
using System.Text;
using System.Text.Json;
using CnjProcessosApi.Core.Models;
using Microsoft.Extensions.Logging;
using CnjProcessosApi.Core.Interfaces;

namespace CnjProcessosApi.Infrastructure.Http;

public class CnjClient : ICnjClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CnjClient> _logger;

    public CnjClient(HttpClient httpClient, ILogger<CnjClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(Processo? processo, bool sigiloso)> BuscarProcessoAsync(string numeroCnj, string tribunalEndpoint, CancellationToken ct = default)
    {
        var url = tribunalEndpoint;

        var query = new
        {
            query = new
            {
                term = new Dictionary<string, object> { { "numeroProcesso.keyword", numeroCnj } }
            },
            size = 10000
        };
        var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, ct);
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Processo {numero} com acesso restrito ou não encontrado.", numeroCnj);
                return (null, true);
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var proc = new Processo
            {
                NumeroCNJ = numeroCnj,
                Movimentacoes = new List<Movimentacao>()
            };
            return (proc, false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro HTTP ao consultar CNJ para {numero}", numeroCnj);
            throw;
        }
    }
}
