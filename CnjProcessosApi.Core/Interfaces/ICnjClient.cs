using CnjProcessosApi.Core.Models;

namespace CnjProcessosApi.Core.Interfaces;

public interface ICnjClient
{
    Task<(Processo? processo, bool sigiloso)> BuscarProcessoAsync(string numeroCnj, string tribunalEndpoint, CancellationToken ct = default);
}
