namespace GestaoFinanceira.Models;

public sealed record FluxoCaixaQuery(DateOnly? DataInicial = null, DateOnly? DataFinal = null,
    long? EmpresaId = null, long? ContaBancariaId = null, long? CategoriaId = null,
    long? CentroCustoId = null, string? Tipo = null, int Pagina = 1, int TamanhoPagina = 20)
{
    public Dictionary<string, string[]> Validar(string consulta)
    {
        var erros = new Dictionary<string, string[]>();
        if (DataInicial is null || DataFinal is null || DataInicial > DataFinal)
            erros["Datas"] = ["Informe dataInicial e dataFinal válidas, em ordem crescente (yyyy-MM-dd)."];
        if (Tipo is not (null or "RECEITA" or "DESPESA")) erros[nameof(Tipo)] = ["Tipo inválido."];
        foreach (var (campo, id) in new[] { (nameof(EmpresaId), EmpresaId), (nameof(ContaBancariaId), ContaBancariaId),
            (nameof(CategoriaId), CategoriaId), (nameof(CentroCustoId), CentroCustoId) })
            if (id <= 0) erros[campo] = ["O ID deve ser positivo."];
        if (Pagina < 1 || TamanhoPagina is < 1 or > 100 || ((long)Pagina - 1) * TamanhoPagina > int.MaxValue)
            erros[nameof(Pagina)] = ["Página positiva, tamanho entre 1 e 100 e deslocamento até Int32.MaxValue."];
        if (ContaBancariaId.HasValue && consulta is not ("realizado" or "por-conta"))
            erros[nameof(ContaBancariaId)] = ["Conta bancária só é atribuível ao realizado. Utilize realizado ou por-conta."];
        return erros;
    }
}

public sealed record FluxoMovimentoResponse(long LancamentoId, long? BaixaId, long? ContaBancariaId,
    string Tipo, string Descricao, long CategoriaId, long? CentroCustoId, DateTime Data,
    decimal Principal, decimal Juros, decimal Multa, decimal Desconto);
public sealed record FluxoPagina<T>(long EmpresaId, int Pagina, int TamanhoPagina, int Total, IReadOnlyList<T> Itens);
public sealed record FluxoResumoResponse(long EmpresaId, DateOnly DataInicial, DateOnly DataFinal,
    decimal ReceitasPrevistas, decimal DespesasPrevistas, decimal ReceitasRealizadas, decimal DespesasRealizadas,
    decimal SaldoPrevisto, decimal SaldoRealizado, decimal ValoresPendentes,
    int QuantidadeLancamentos, int QuantidadeBaixasValidas);
public sealed record FluxoPeriodoResponse(DateTime Data, decimal ReceitasPrevistas, decimal DespesasPrevistas,
    decimal ReceitasRealizadas, decimal DespesasRealizadas, decimal SaldoPrevisto, decimal SaldoRealizado);
public sealed record FluxoContaResponse(long ContaBancariaId, decimal ReceitasRealizadas, decimal DespesasRealizadas,
    decimal SaldoRealizado, decimal? SaldoBancarioAcumulado);
public sealed record FluxoDashboardResponse(FluxoResumoResponse Resumo, FluxoPagina<FluxoPeriodoResponse> PorPeriodo,
    FluxoPagina<FluxoContaResponse> PorConta);
