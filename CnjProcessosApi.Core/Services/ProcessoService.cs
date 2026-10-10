using CnjProcessosApi.Core.DTOs;
using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CnjProcessosApi.Core.Services;

public class ProcessoService(IProcessoRepository repo, ICnjClient cnjClient, ILogger<ProcessoService> logger, IConfiguration config) : IProcessoService
{
    // Serialize refreshes within this single-instance SQLite application.
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public async Task<ProcessoResponseDto?> GetDetalheAsync(string numeroCnj, CancellationToken ct = default, string? apiKey = null)
    {
        numeroCnj = NumeroCnj.Normalizar(numeroCnj);
        var endpoint = NumeroCnj.Tribunal(numeroCnj);
        if (apiKey is not null)
        {
            // Visitor credentials and their responses never enter the shared cache.
            var (personal, restricted) = await cnjClient.BuscarProcessoAsync(numeroCnj, endpoint, ct, apiKey);
            if (personal is null || restricted) return null;
            foreach (var movement in personal.Movimentacoes) movement.Descricao = Anon(movement.Descricao);
            personal.UltimaAtualizacao = DateTime.UtcNow;
            return Map(personal);
        }
        await RefreshLock.WaitAsync(ct);
        try
        {
            var proc = await repo.GetByNumeroAsync(numeroCnj, ct);
            var ttl = int.TryParse(config["DataJud:CacheMinutes"], out var minutes) ? Math.Clamp(minutes, 0, 1440) : 60;
            if (proc == null || proc.UltimaAtualizacao == null || proc.UltimaAtualizacao < DateTime.UtcNow.AddMinutes(-ttl))
                proc = await AtualizarAsync(numeroCnj, endpoint, ct);
            return proc is null || proc.IsSigiloso ? null : Map(proc);
        }
        finally { RefreshLock.Release(); }
    }

    public async Task<(IEnumerable<ProcessoResponseDto> items, int total)> GetListaAsync(int page, int pageSize, string? filtroNumero = null, CancellationToken ct = default)
    {
        if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 100)
            throw new ArgumentException("Use page entre 1 e 1000000 e pageSize entre 1 e 100.");
        if (!string.IsNullOrWhiteSpace(filtroNumero))
        {
            filtroNumero = filtroNumero.Trim().Replace(".", "").Replace("-", "");
            if (filtroNumero.Length > 20 || filtroNumero.Any(c => c is < '0' or > '9'))
                throw new ArgumentException("O filtro deve conter até 20 dígitos do número CNJ.");
        }
        var (items, total) = await repo.GetPagedAsync(page, pageSize, filtroNumero, ct);
        return (items.Select(Map).ToList(), total);
    }

    public async Task SyncAsync(IEnumerable<string> numerosCnj, string tribunalEndpoint, CancellationToken ct = default)
    {
        // Validate the whole batch before making external requests.
        var numeros = numerosCnj.Select(NumeroCnj.Normalizar).Distinct().ToArray();
        var consultas = numeros.Select(n => (Numero: n, Endpoint: NumeroCnj.Tribunal(n))).ToArray();
        await RefreshLock.WaitAsync(ct);
        try
        {
            foreach (var consulta in consultas)
            {
                ct.ThrowIfCancellationRequested();
                await AtualizarAsync(consulta.Numero, consulta.Endpoint, ct);
            }
            logger.LogInformation("Sincronização concluída: {Count} consultas", consultas.Length);
        }
        finally { RefreshLock.Release(); }
    }

    private async Task<Processo?> AtualizarAsync(string numero, string endpoint, CancellationToken ct)
    {
        var (proc, sigiloso) = await cnjClient.BuscarProcessoAsync(numero, endpoint, ct);
        if (sigiloso || proc == null)
        {
            // Remove cached details when a previously public record disappears or becomes restricted.
            await repo.RemoveAsync(numero, ct);
            return null;
        }
        proc.Movimentacoes = proc.Movimentacoes.Select(m => new Movimentacao
        {
            Data = m.Data, Descricao = Anon(m.Descricao)
        }).ToList();
        proc.UltimaAtualizacao = DateTime.UtcNow;
        proc.NomePartesAnonimizados = null;
        await repo.AddOrUpdateAsync(proc, ct);
        return proc;
    }

    private ProcessoResponseDto Map(Processo p)
    {
        var movimentos = p.Movimentacoes.OrderByDescending(m => m.Data)
            .Select(m => new MovimentacaoDto { Data = m.Data, Descricao = m.Descricao }).ToList();
        return new ProcessoResponseDto
        {
            NumeroCNJ = p.NumeroCNJ, Classe = p.Classe, Assunto = p.Assunto,
            OrgaoJulgador = p.OrgaoJulgador, IsSigiloso = false,
            UltimaAtualizacao = p.UltimaAtualizacao, Movimentacoes = movimentos,
            UltimaMovimentacao = movimentos.FirstOrDefault(),
            Disclaimer = config["Legal:Disclaimer"] ?? "Metadados públicos do DataJud. Confira as informações no tribunal de origem."
        };
    }

    private static string Anon(string text)
    {
        var masked = System.Text.RegularExpressions.Regex.Replace(text, @"\b(?:\d{3}\.?){2}\d{3}-?\d{2}\b", "***CPF***");
        return System.Text.RegularExpressions.Regex.Replace(masked, @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", "***CNPJ***");
    }
}
