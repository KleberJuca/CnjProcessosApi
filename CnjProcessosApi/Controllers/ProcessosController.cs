using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? filtro = null, CancellationToken ct = default)
    {
        var (items, total) = await _service.GetListaAsync(page, pageSize, filtro, ct);
        return Ok(new { total, items });
    }

    [HttpGet("{numeroCNJ}")]
    [Authorize]
    public async Task<IActionResult> GetByNumero(string numeroCNJ, CancellationToken ct)
    {
        var item = await _service.GetDetalheAsync(numeroCNJ, ct);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var numeros = _config.GetSection("Processos:NumerosCNJ").Get<string[]>() ?? Array.Empty<string>();
        var endpoint = _config["DataJud:TribunalEndpoint"] ?? "api_publica_tjsp/_search";
        await _service.SyncAsync(numeros, endpoint, ct);
        return Ok(new { message = "Sincronização iniciada" });
    }
}
