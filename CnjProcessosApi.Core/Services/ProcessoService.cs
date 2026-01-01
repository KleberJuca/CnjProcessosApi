using CnjProcessosApi.Core.DTOs;
using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CnjProcessosApi.Core.Services;

public class ProcessoService : IProcessoService
{
    private readonly IProcessoRepository _repo;
    private readonly ICnjClient _cnjClient;
    private readonly ILogger<ProcessoService> _logger;
    private readonly IConfiguration _config;

    public ProcessoService(IProcessoRepository repo, ICnjClient cnjClient, ILogger<ProcessoService> logger, IConfiguration config)
    {
        _repo = repo;
        _cnjClient = cnjClient;
        _logger = logger;
        _config = config;
    }

    public async Task<ProcessoResponseDto?> GetDetalheAsync(string numeroCnj, CancellationToken ct = default)
    {
        var proc = await _repo.GetByNumeroAsync(numeroCnj, ct);
        if (proc == null) return null;
        var dto = MapWithUltimaMov(proc);
        dto.Disclaimer = _config["Legal:Disclaimer"] ?? dto.Disclaimer;
        return dto;
    }

    public async Task<(IEnumerable<ProcessoResponseDto> items, int total)> GetListaAsync(int page, int pageSize, string? filtroNumero = null, CancellationToken ct = default)
    {
        var (items, total) = await _repo.GetPagedAsync(page, pageSize, filtroNumero, ct);
        var mapped = items.Select(p => {
            var dto = MapWithUltimaMov(p);
            dto.Disclaimer = _config["Legal:Disclaimer"] ?? dto.Disclaimer;
            return dto;
        });
        return (mapped, total);
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
                    // Não persistir detalhes, apenas marcar sigilo se já existir registro
                    var existing = await _repo.GetByNumeroAsync(numero, ct);
                    if (existing != null)
                    {
                        existing.IsSigiloso = true;
                        existing.UltimaAtualizacao = DateTime.UtcNow;
                        await _repo.AddOrUpdateAsync(existing, ct);
                    }
                    continue;
                }
                if (procFromCnj != null)
                {
                    // Ordenar movimentações desc e anonimizar possíveis nomes nas descrições se aplicável
                    procFromCnj.Movimentacoes = procFromCnj.Movimentacoes
                        .OrderByDescending(m => m.Data)
                        .Select(m => new Movimentacao { Data = m.Data, Descricao = Anon(m.Descricao), ProcessoId = procFromCnj.Id, Processo = procFromCnj })
                        .ToList();
                    procFromCnj.UltimaAtualizacao = DateTime.UtcNow;
                    procFromCnj.NomePartesAnonimizados = null; // manter vazio por padrão
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
        var movs = p.Movimentacoes.OrderByDescending(m => m.Data)
            .Select(m => new MovimentacaoDto { Data = m.Data, Descricao = m.Descricao })
            .ToList();
        var dto = new ProcessoResponseDto
        {
            NumeroCNJ = p.NumeroCNJ,
            Classe = p.Classe,
            Assunto = p.Assunto,
            OrgaoJulgador = p.OrgaoJulgador,
            IsSigiloso = p.IsSigiloso,
            UltimaAtualizacao = p.UltimaAtualizacao,
            Movimentacoes = movs
        };
        dto.UltimaMovimentacao = movs.FirstOrDefault();
        return dto;
    }

    private string Anon(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        // Estratégia simples: mascarar padrões de CPF/CNPJ (não armazenar) e suprimir nomes entre aspas
        var masked = System.Text.RegularExpressions.Regex.Replace(text, @"\b\d{3}\.\d{3}\.\d{3}\-\d{2}\b", "***CPF***");
        masked = System.Text.RegularExpressions.Regex.Replace(masked, @"\b\d{2}\.\d{3}\.\d{3}\/(\d{4})\-\d{2}\b", "***CNPJ***");
        return masked;
    }
}
