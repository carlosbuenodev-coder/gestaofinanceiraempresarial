namespace GestaoFinanceira.Models;

// Read-only projection. There is no endpoint or EF write path for payments.
public sealed class BaixaFinanceira
{
    public long Id { get; set; }
    public long LancamentoId { get; set; }
}
