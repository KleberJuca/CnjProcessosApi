namespace CnjProcessosApi.Core.DTOs;

public class MovimentacaoDto
{
    public DateTime Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

public class ProcessoResponseDto
{
    public string NumeroCNJ { get; set; } = string.Empty;
    public string? Classe { get; set; }
    public string? Assunto { get; set; }
    public string? OrgaoJulgador { get; set; }
    public bool IsSigiloso { get; set; }
    public DateTime? UltimaAtualizacao { get; set; }
    public MovimentacaoDto? UltimaMovimentacao { get; set; }
    public IEnumerable<MovimentacaoDto> Movimentacoes { get; set; } = Enumerable.Empty<MovimentacaoDto>();
}
