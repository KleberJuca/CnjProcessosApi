using Quartz;
using CnjProcessosApi.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CnjProcessosApi.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class ProcessoSyncJob : IJob
{
    private readonly IConfiguration _config;
    private readonly IProcessoService _service;

    public ProcessoSyncJob(IConfiguration config, IProcessoService service)
    {
        _config = config;
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var numeros = _config.GetSection("Processos:NumerosCNJ").GetChildren().Select(c => c.Value ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        var endpoint = _config["DataJud:TribunalEndpoint"] ?? "api_publica_tjsp/_search";
        await _service.SyncAsync(numeros, endpoint, context.CancellationToken);
    }
}
