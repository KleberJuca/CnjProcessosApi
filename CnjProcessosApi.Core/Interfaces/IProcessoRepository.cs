using CnjProcessosApi.Core.Models;

namespace CnjProcessosApi.Core.Interfaces;

public interface IProcessoRepository
{
    Task RemoveAsync(string numeroCnj, CancellationToken ct = default);
    Task<Processo?> GetByNumeroAsync(string numeroCnj, CancellationToken ct = default);
    Task<(IEnumerable<Processo> items, int total)> GetPagedAsync(int page, int pageSize, string? filtroNumero = null, CancellationToken ct = default);
    Task AddOrUpdateAsync(Processo processo, CancellationToken ct = default);
}
