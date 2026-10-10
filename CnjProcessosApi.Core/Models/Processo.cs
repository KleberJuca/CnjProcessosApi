namespace CnjProcessosApi.Core.Models;

public class Processo
{
    public int Id { get; set; }
    public string NumeroCNJ { get; set; } = string.Empty;
    public string? Classe { get; set; }
    public string? Assunto { get; set; }
    public string? OrgaoJulgador { get; set; }
    public bool IsSigiloso { get; set; }
    public DateTime? UltimaAtualizacao { get; set; }
    // Campo opcional com nomes anonimizados (ex.: iniciais), nunca dados sensíveis
    public string? NomePartesAnonimizados { get; set; }
    public ICollection<Movimentacao> Movimentacoes { get; set; } = new List<Movimentacao>();
}

public class Movimentacao
{
    public int Id { get; set; }
    public int ProcessoId { get; set; }
    public Processo Processo { get; set; } = null!;
    public DateTime Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
}
