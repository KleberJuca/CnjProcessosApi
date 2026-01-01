using CnjProcessosApi.Core.DTOs;
using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace CnjProcessosApi.Core.Services;

public class ProcessoService : IProcessoService
{
    private readonly IProcessoRepository _repo;
    private readonly ICnjClient _cnjClient;
    private readonly ILogger<ProcessoService> _logger;

    public ProcessoService(IProcessoRepository repo, ICnjClient cnjClient, ILogger<ProcessoService> logger)
    {
        _repo = repo;
        _cnjClient = cnjClient;
        _logger = logger;
    }

    public async Task<ProcessoResponseDto?> GetDetalheAsync(string numeroCnj, CancellationToken ct = default)
    {
        var proc = await _repo.GetByNumeroAsync(numeroCnj, ct);
        if (proc == null) return null;
        var dto = MapWithUltimaMov(proc);
        return dto;
    }

    public async Task<(IEnumerable<ProcessoResponseDto> items, int total)> GetListaAsync(int page, int pageSize, string? filtroNumero = null, CancellationToken ct = default)
    {
        var (items, total) = await _repo.GetPagedAsync(page, pageSize, filtroNumero, ct);
        return (items.Select(MapWithUltimaMov), total);
    }

    public async Task SyncAsync(IEnumerable<string> numerosCnj, string tribunalEndpoint, CancellationToken ct = default)
    {
        foreach (var numero in numerosCnj)
        {
            try
            {
                var result = await _cnjClient.BuscarProcessoAsync(numero, tribunalEndpoint, ct);
                var procFromCnj = result.processo;
                var sigiloso = result.sigiloso;
                if (sigiloso)
                {
                    var existing = await _repo.GetByNumeroAsync(numero, ct) ?? new Processo { NumeroCNJ = numero };
                    existing.IsSigiloso = true;
                    existing.UltimaAtualizacao = DateTime.UtcNow;
                    await _repo.AddOrUpdateAsync(existing, ct);
                    continue;
                }
                if (procFromCnj != null)
                {
                    procFromCnj.Movimentacoes = procFromCnj.Movimentacoes
                        .OrderByDescending(m => m.Data)
                        .ToList();
                    procFromCnj.UltimaAtualizacao = DateTime.UtcNow;
                    await _repo.AddOrUpdateAsync(procFromCnj, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar processo {numero}", numero);
            }
        }
    }

    private ProcessoResponseDto MapWithUltimaMov(Processo p)
    {
        var dto = new ProcessoResponseDto
        {
            NumeroCNJ = p.NumeroCNJ,
            Classe = p.Classe,
            Assunto = p.Assunto,
            OrgaoJulgador = p.OrgaoJulgador,
            IsSigiloso = p.IsSigiloso,
            UltimaAtualizacao = p.UltimaAtualizacao,
            Movimentacoes = p.Movimentacoes.OrderByDescending(m => m.Data)
                .Select(m => new MovimentacaoDto { Data = m.Data, Descricao = m.Descricao })
                .ToList()
        };
        dto.UltimaMovimentacao = dto.Movimentacoes.FirstOrDefault();
        return dto;
    }
}
