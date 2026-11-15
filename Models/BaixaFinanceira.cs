namespace GestaoFinanceira.Models;

public sealed class BaixaFinanceira
{
    public long Id { get; set; }
    public long LancamentoId { get; set; }
    public long ContaBancariaId { get; set; }
    public long? FormaPagamentoId { get; set; }
    public DateTime DataPagamento { get; set; }
    public decimal Valor { get; set; }
    public decimal Juros { get; set; }
    public decimal Multa { get; set; }
    public decimal Desconto { get; set; }
    public string? Observacoes { get; set; }
}

public sealed class EstornoBaixa
{
    public long Id { get; set; }
    public long BaixaId { get; set; }
    public string Motivo { get; set; } = "";
    public DateTime CriadoEm { get; set; }
}

public sealed class ContaBancariaFinanceira : IReferenciaFinanceira
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
    public bool? Ativo { get; set; }
}

public sealed class FormaPagamentoFinanceira
{
    public long Id { get; set; }
}
