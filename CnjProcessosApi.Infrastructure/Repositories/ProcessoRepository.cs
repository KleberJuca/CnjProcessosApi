using CnjProcessosApi.Core.Interfaces;
using CnjProcessosApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using CnjProcessosApi.Infrastructure.Persistence;

namespace CnjProcessosApi.Infrastructure.Repositories;

public class ProcessoRepository : IProcessoRepository
{
    public async Task RemoveAsync(string numeroCnj, CancellationToken ct = default)
    {
        var processo = await _db.Processos.FirstOrDefaultAsync(p => p.NumeroCNJ == numeroCnj, ct);
        if (processo == null) return;
        _db.Processos.Remove(processo);
        await _db.SaveChangesAsync(ct);
    }
    private readonly AppDbContext _db;

    public ProcessoRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Processo?> GetByNumeroAsync(string numeroCnj, CancellationToken ct = default)
    {
        return await _db.Processos.AsNoTracking()
            .Include(p => p.Movimentacoes)
            .FirstOrDefaultAsync(p => p.NumeroCNJ == numeroCnj, ct);
    }

    public async Task<(IEnumerable<Processo> items, int total)> GetPagedAsync(int page, int pageSize, string? filtroNumero = null, CancellationToken ct = default)
    {
        var query = _db.Processos.AsNoTracking().Include(p => p.Movimentacoes).Where(p => !p.IsSigiloso);
        if (!string.IsNullOrWhiteSpace(filtroNumero))
        {
            query = query.Where(p => p.NumeroCNJ.Contains(filtroNumero));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.UltimaAtualizacao)
            .ThenBy(p => p.NumeroCNJ)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AddOrUpdateAsync(Processo processo, CancellationToken ct = default)
    {
        var existing = await _db.Processos.Include(p => p.Movimentacoes)
            .FirstOrDefaultAsync(p => p.NumeroCNJ == processo.NumeroCNJ, ct);
        if (existing == null)
        {
            await _db.Processos.AddAsync(processo, ct);
        }
        else
        {
            existing.Classe = processo.Classe;
            existing.Assunto = processo.Assunto;
            existing.OrgaoJulgador = processo.OrgaoJulgador;
            existing.IsSigiloso = processo.IsSigiloso;
            existing.UltimaAtualizacao = processo.UltimaAtualizacao;
            // Replace movimentacoes (simple approach)
            _db.Movimentacoes.RemoveRange(existing.Movimentacoes);
            existing.NomePartesAnonimizados = null;
            existing.Movimentacoes = processo.Movimentacoes.Select(m => new Movimentacao
            {
                Data = m.Data, Descricao = m.Descricao
            }).ToList();
        }
        await _db.SaveChangesAsync(ct);
    }
}
