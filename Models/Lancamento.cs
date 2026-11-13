namespace GestaoFinanceira.Models;

public sealed class Lancamento
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
    public string Tipo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public long? ClienteId { get; set; }
    public long? FornecedorId { get; set; }
    public long CategoriaId { get; set; }
    public long? CentroCustoId { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime DataVencimento { get; set; }
    public decimal Valor { get; set; }
    public string Status { get; set; } = "ABERTO";
    public string? Observacoes { get; set; }
    public DateTime? CriadoEm { get; set; }
}
