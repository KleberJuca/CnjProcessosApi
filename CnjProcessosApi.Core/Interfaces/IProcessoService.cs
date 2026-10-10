using CnjProcessosApi.Core.DTOs;
using CnjProcessosApi.Core.Models;

namespace CnjProcessosApi.Core.Interfaces;

public interface IProcessoService
{
    Task<ProcessoResponseDto?> GetDetalheAsync(string numeroCnj, CancellationToken ct = default, string? apiKey = null);
    Task<(IEnumerable<ProcessoResponseDto> items, int total)> GetListaAsync(int page, int pageSize, string? filtroNumero = null, CancellationToken ct = default);
    Task SyncAsync(IEnumerable<string> numerosCnj, string tribunalEndpoint, CancellationToken ct = default);
}
