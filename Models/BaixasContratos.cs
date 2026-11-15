using System.Text.Json.Serialization;

namespace GestaoFinanceira.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriarBaixaRequest(long? ContaBancariaId = null, long? FormaPagamentoId = null,
    DateOnly? DataPagamento = null, decimal? Valor = null, string? Observacoes = null, long? EmpresaId = null)
{
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();
        if (ContaBancariaId is null or <= 0) erros[nameof(ContaBancariaId)] = ["Conta bancária obrigatória e positiva."];
        if (FormaPagamentoId <= 0) erros[nameof(FormaPagamentoId)] = ["Forma de pagamento deve ser positiva."];
        if (EmpresaId <= 0) erros[nameof(EmpresaId)] = ["Empresa deve ser positiva."];
        if (DataPagamento is null || DataPagamento.Value.Year < 1000)
            erros[nameof(DataPagamento)] = ["Data de pagamento obrigatória, entre 1000-01-01 e 9999-12-31."];
        if (Valor is null or <= 0 or > 9999999999999.99m ||
            (Valor.HasValue && decimal.Round(Valor.Value, 2) != Valor.Value))
            erros[nameof(Valor)] = ["Valor positivo, compatível com decimal(15,2) e no máximo duas casas decimais."];
        if (Observacoes?.Length > 255) erros[nameof(Observacoes)] = ["Observações limitadas a 255 caracteres."];
        return erros;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EstornarBaixaRequest(string? Motivo = null, long? EmpresaId = null)
{
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Motivo) || Motivo.Trim().Length > 500)
            erros[nameof(Motivo)] = ["Motivo obrigatório, com até 500 caracteres."];
        if (EmpresaId <= 0) erros[nameof(EmpresaId)] = ["Empresa deve ser positiva."];
        return erros;
    }
}

public sealed record BaixaResponse(long Id, long EmpresaId, long LancamentoId, long ContaBancariaId,
    long? FormaPagamentoId, DateTime DataPagamento, decimal Valor, decimal Juros, decimal Multa,
    decimal Desconto, string? Observacoes, bool Estornada, long? EstornoId, string? MotivoEstorno,
    DateTime? EstornadaEm);

public sealed record SaldoLancamentoResponse(long LancamentoId, long EmpresaId, decimal ValorOriginal,
    decimal TotalBaixado, decimal SaldoRestante, string Status);

public sealed record MovimentoBaixaResponse(BaixaResponse Baixa, SaldoLancamentoResponse Saldo);
public sealed record PaginaBaixas(IReadOnlyList<BaixaResponse> Itens, long Total, int Pagina, int TamanhoPagina);
