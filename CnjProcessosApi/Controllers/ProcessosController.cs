using Microsoft.AspNetCore.Mvc;
using CnjProcessosApi.Core.Interfaces;

namespace CnjProcessosApi.Controllers;

[ApiController]
[Route("api/processos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
        if (!_config.GetValue<bool>("Processos:EnableManualSync"))
            return StatusCode(403, new { message = "Sincronização manual desabilitada. Use a consulta por número ou o agendamento." });
        var numeros = _config.GetSection("Processos:NumerosCNJ").GetChildren().Select(c => c.Value ?? string.Empty).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        var endpoint = _config["DataJud:TribunalEndpoint"] ?? "api_publica_tjsp/_search";
        await _service.SyncAsync(numeros, endpoint, ct);
        return Ok(new { message = "Sincronização concluída", disclaimer = _config["Legal:Disclaimer"] });
    }

    [HttpPost("consulta")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Consulta([FromBody] ConsultaRequest request, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey;
        var item = await _service.GetDetalheAsync(request.NumeroCNJ, ct, key);
        return item is null
            ? NotFound(new { message = "Processo não encontrado ou não disponível", disclaimer = _config["Legal:Disclaimer"] })
            : Ok(item);
    }
}

public sealed class ConsultaRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(25)]
    public string NumeroCNJ { get; set; } = "";
    [System.ComponentModel.DataAnnotations.StringLength(2048)]
    public string? ApiKey { get; set; }
}
