using System.Text;
using System.Text.Json.Serialization;

namespace GestaoFinanceira.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CriarLancamentoRequest(
    string? Tipo = null, string? Descricao = null, long? CategoriaId = null,
    long? ClienteId = null, long? FornecedorId = null, long? CentroCustoId = null,
    DateOnly? DataEmissao = null, DateOnly? DataVencimento = null, decimal? Valor = null,
    string? Observacoes = null, long? EmpresaId = null)
{
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();
        void Erro(string campo, string mensagem) => erros[campo] = [mensagem];
        if (Tipo is not ("RECEITA" or "DESPESA")) Erro(nameof(Tipo), "Informe RECEITA ou DESPESA.");
        if (string.IsNullOrWhiteSpace(Descricao) || Descricao.Trim().Length > 255)
            Erro(nameof(Descricao), "Descrição obrigatória, com até 255 caracteres.");
        if (CategoriaId is null or <= 0) Erro(nameof(CategoriaId), "Categoria obrigatória e positiva.");
        foreach (var (campo, valor) in new[] { (nameof(ClienteId), ClienteId),
            (nameof(FornecedorId), FornecedorId), (nameof(CentroCustoId), CentroCustoId), (nameof(EmpresaId), EmpresaId) })
            if (valor <= 0) Erro(campo, "ID deve ser positivo.");
        if (DataEmissao is null || DataEmissao.Value.Year < 1000) Erro(nameof(DataEmissao), "Data de emissão obrigatória, entre 1000-01-01 e 9999-12-31.");
        if (DataVencimento is null || DataVencimento.Value.Year < 1000) Erro(nameof(DataVencimento), "Data de vencimento obrigatória, entre 1000-01-01 e 9999-12-31.");
        if (Valor is null or <= 0 or > 9999999999999.99m || (Valor.HasValue && decimal.Round(Valor.Value, 2) != Valor.Value))
            Erro(nameof(Valor), "Valor positivo, até 9999999999999,99 e com no máximo duas casas decimais.");
        if (Observacoes is not null && Encoding.UTF8.GetByteCount(Observacoes) > 65535)
            Erro(nameof(Observacoes), "Observações excedem o limite da coluna TEXT.");
        return erros;
    }
}
