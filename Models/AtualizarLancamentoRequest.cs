using System.Text.Json.Serialization;

namespace GestaoFinanceira.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AtualizarLancamentoRequest(
    string? Descricao = null, long? CategoriaId = null, long? ClienteId = null,
    long? FornecedorId = null, long? CentroCustoId = null, DateOnly? DataEmissao = null,
    DateOnly? DataVencimento = null, decimal? Valor = null, string? Observacoes = null,
    long? EmpresaId = null)
{
    // Reuse the same limits as creation. Tipo always comes from the persisted entry.
    public CriarLancamentoRequest ComoCadastro(string tipo) => new(tipo, Descricao, CategoriaId,
        ClienteId, FornecedorId, CentroCustoId, DataEmissao, DataVencimento, Valor, Observacoes, EmpresaId);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelarLancamentoRequest(long? EmpresaId = null);
