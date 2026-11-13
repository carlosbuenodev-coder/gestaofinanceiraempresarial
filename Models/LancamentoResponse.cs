namespace GestaoFinanceira.Models;

public sealed record LancamentoResponse(long Id, long EmpresaId, string Tipo, string Descricao,
    long? ClienteId, long? FornecedorId, long CategoriaId, long? CentroCustoId,
    DateTime DataEmissao, DateTime DataVencimento, decimal Valor, string Status,
    string? Observacoes, DateTime? CriadoEm);

public sealed record PaginaLancamentos(IReadOnlyList<LancamentoResponse> Itens, long Total,
    int Pagina, int TamanhoPagina, long TotalPaginas);
