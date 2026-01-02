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
        // Usa caminho relativo combinado com BaseAddress do HttpClient
        var url = tribunalEndpoint?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("TribunalEndpoint não configurado");
            return (null, false);
        }

        var query = new
        {
            query = new
            {
                term = new Dictionary<string, object> { { "numeroProcesso.keyword", numeroCnj } }
            },
            size = 500 // reduzir payload
        };
        var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, ct);

            // 401: falta/erro de APIKey
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var body401 = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Unauthorized ao consultar CNJ. Verifique DataJud:ApiKey e cabeçalho Authorization. Body: {body}", body401);
                return (null, false);
            }

            // Tratamento explícito de sigilo
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Processo {numero} com acesso restrito ou não encontrado.", numeroCnj);
                return (null, true);
            }

            // Demais códigos não-sucesso
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Falha ao consultar CNJ para {numero}. Status: {status}. Body: {body}", numeroCnj, (int)response.StatusCode, body);
                return (null, false);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            // TODO: Mapear schema real do DataJud. Mantém estrutura mínima.
            var proc = new Processo
            {
                NumeroCNJ = numeroCnj,
                Movimentacoes = new List<Movimentacao>()
            };
            return (proc, false);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout ao consultar CNJ para {numero}", numeroCnj);
            return (null, false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro HTTP ao consultar CNJ para {numero}", numeroCnj);
            return (null, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao consultar CNJ para {numero}", numeroCnj);
            return (null, false);
        }
    }
}
