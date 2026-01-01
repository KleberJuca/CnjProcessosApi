using Microsoft.AspNetCore.Mvc;
using CnjProcessosApi.Core.Interfaces;

namespace CnjProcessosApi.Controllers;

[ApiController]
[Route("api/processos")]
public class ProcessosController : ControllerBase
{
    private readonly IProcessoService _service;
    private readonly IConfiguration _config;

    public ProcessosController(IProcessoService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? filtro = null, CancellationToken ct = default)
    {
        var (items, total) = await _service.GetListaAsync(page, pageSize, filtro, ct);
        var disclaimer = _config["Legal:Disclaimer"] ?? "Dados do DataJud 'as is'. Uso gratuito e não comercial conforme termos do CNJ.";
        return Ok(new { total, items, disclaimer });
    }

    [HttpGet("{numeroCNJ}")]
    public async Task<IActionResult> GetByNumero(string numeroCNJ, CancellationToken ct)
    {
        var item = await _service.GetDetalheAsync(numeroCNJ, ct);
        if (item == null) return NotFound(new { message = "Processo não encontrado ou não disponível", disclaimer = _config["Legal:Disclaimer"] });
        return Ok(item);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var numeros = _config.GetSection("Processos:NumerosCNJ").GetChildren().Select(c => c.Value ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        var endpoint = _config["DataJud:TribunalEndpoint"] ?? "api_publica_tjsp/_search";
        await _service.SyncAsync(numeros, endpoint, ct);
        return Ok(new { message = "Sincronização solicitada", disclaimer = _config["Legal:Disclaimer"] });
    }
}
