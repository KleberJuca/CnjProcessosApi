using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CnjProcessosApi.Core.Models;
using CnjProcessosApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CnjProcessosApi.Infrastructure.Http;

public class CnjClient(HttpClient httpClient, ILogger<CnjClient> logger) : ICnjClient
{
    public async Task<(Processo? processo, bool sigiloso)> BuscarProcessoAsync(string numeroCnj, string tribunalEndpoint, CancellationToken ct = default, string? apiKey = null)
    {
        numeroCnj = NumeroCnj.Normalizar(numeroCnj);
        if (!Regex.IsMatch(tribunalEndpoint, @"^api_publica_[a-z0-9-]+/_search$"))
            throw new ArgumentException("Endpoint de tribunal inválido.");
        if (apiKey is not null)
        {
            apiKey = apiKey.Trim();
            if (apiKey.StartsWith("APIKey ", StringComparison.OrdinalIgnoreCase)) apiKey = apiKey[7..].Trim();
            if (!Regex.IsMatch(apiKey, @"^[A-Za-z0-9+/=_-]{8,2048}$"))
                throw new ArgumentException("A chave DataJud informada tem formato inválido.");
            if (httpClient.BaseAddress?.Scheme != "https" || httpClient.BaseAddress.Host != "api-publica.datajud.cnj.jus.br")
                throw new DataJudException("Chaves de visitantes só podem ser enviadas ao endereço oficial do DataJud.", 503);
        }
        if (apiKey is null && httpClient.DefaultRequestHeaders.Authorization is not { Parameter.Length: > 0 })
            throw new DataJudException("Configure DataJud:ApiKey com a chave pública disponível na Wiki do CNJ.", 503);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, tribunalEndpoint);
            if (apiKey is not null)
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("APIKey", apiKey);
            request.Content = JsonContent.Create(new
            {
                query = new { match = new { numeroProcesso = numeroCnj } }, size = 100,
                sort = new[] { new { dataHoraUltimaAtualizacao = new { order = "desc" } } }
            });
            using var response = await httpClient.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new DataJudException("O DataJud recusou o acesso. Confira a chave pública configurada.", 503);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new DataJudException("Limite do DataJud atingido. Tente novamente mais tarde.", 503);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("DataJud retornou HTTP {StatusCode}", (int)response.StatusCode);
                throw new DataJudException("Não foi possível consultar o tribunal no DataJud. Tente novamente mais tarde.");
            }
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = document.RootElement;
            if ((root.TryGetProperty("timed_out", out var timedOut) && timedOut.ValueKind == JsonValueKind.True) ||
                (root.TryGetProperty("_shards", out var shards) && shards.TryGetProperty("failed", out var failed) && failed.GetInt32() > 0))
                throw new DataJudException("O DataJud retornou uma consulta incompleta. Tente novamente.");
            var sources = root.GetProperty("hits").GetProperty("hits").EnumerateArray()
                .Select(h => h.GetProperty("_source"))
                .Where(s => Text(s, "numeroProcesso") == numeroCnj).ToList();
            if (sources.Count == 0) return (null, false);
            if (sources.Any(s => !s.TryGetProperty("nivelSigilo", out var level) || !level.TryGetInt32(out var n) || n != 0))
                return (null, true);
            var source = sources.OrderByDescending(s => Date(s, "dataHoraUltimaAtualizacao")).First();
            var movimentos = new List<Movimentacao>();
            foreach (var s in sources)
                if (s.TryGetProperty("movimentos", out var array) && array.ValueKind == JsonValueKind.Array)
                    foreach (var m in array.EnumerateArray())
                        if (Date(m, "dataHora") is { } data && Text(m, "nome") is { } nome)
                            movimentos.Add(new Movimentacao { Data = data, Descricao = nome });
            return (new Processo
            {
                NumeroCNJ = numeroCnj, Classe = ObjectName(source, "classe"),
                OrgaoJulgador = ObjectName(source, "orgaoJulgador"),
                Assunto = source.TryGetProperty("assuntos", out var assuntos) ? string.Join("; ", Names(assuntos).Distinct()) : null,
                UltimaAtualizacao = Date(source, "dataHoraUltimaAtualizacao"),
                Movimentacoes = movimentos.DistinctBy(m => (m.Data, m.Descricao)).OrderByDescending(m => m.Data).ToList()
            }, false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DataJudException("O DataJud demorou para responder. Tente novamente.", 504);
        }
        catch (HttpRequestException) { throw new DataJudException("Não foi possível conectar ao DataJud."); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new DataJudException("O DataJud retornou uma resposta em formato inesperado.");
        }
    }

    private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static string? ObjectName(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? Text(v, "nome") : null;
    private static DateTime? Date(JsonElement e, string name) => DateTimeOffset.TryParse(Text(e, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d.UtcDateTime : null;
    private static IEnumerable<string> Names(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in e.EnumerateArray()) foreach (var name in Names(child)) yield return name;
        }
        else if (e.ValueKind == JsonValueKind.Object && Text(e, "nome") is { } name) yield return name;
    }
}
