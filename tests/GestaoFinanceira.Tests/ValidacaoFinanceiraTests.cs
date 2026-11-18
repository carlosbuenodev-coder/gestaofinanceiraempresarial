using GestaoFinanceira.Models;
using Xunit;

namespace GestaoFinanceira.Tests;

public sealed class ValidacaoFinanceiraTests
{
    private static readonly DateOnly DataValida = new(2026, 11, 18);

    public static TheoryData<decimal, bool> LimitesDoValor => new()
    {
        { 0m, false },
        { 1.001m, false },
        { 10000000000000m, false },
        { 9999999999999.99m, true }
    };

    [Theory]
    [MemberData(nameof(LimitesDoValor))]
    public void ValidarLancamento_AplicaLimitesDoValor(decimal valor, bool aceito)
    {
        var request = new CriarLancamentoRequest(
            Tipo: "RECEITA", Descricao: "Lançamento de teste", CategoriaId: 1,
            DataEmissao: DataValida, DataVencimento: DataValida, Valor: valor);

        var erros = request.Validar();

        if (aceito)
            Assert.Empty(erros);
        else
            Assert.Equal(nameof(CriarLancamentoRequest.Valor), Assert.Single(erros).Key);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    public void ValidarBaixa_ExigeContaBancariaPositiva(long? contaBancariaId, bool aceito)
    {
        var request = new CriarBaixaRequest(ContaBancariaId: contaBancariaId, DataPagamento: DataValida, Valor: 10.50m);

        var erros = request.Validar();

        if (aceito)
            Assert.Empty(erros);
        else
            Assert.Equal(nameof(CriarBaixaRequest.ContaBancariaId), Assert.Single(erros).Key);
    }

    [Theory]
    [InlineData("resumo", false)]
    [InlineData("realizado", true)]
    [InlineData("por-conta", true)]
    public void ValidarFluxo_FiltroContaSomenteNoRealizado(string consulta, bool aceito)
    {
        var query = new FluxoCaixaQuery(DataInicial: DataValida, DataFinal: DataValida, ContaBancariaId: 1);

        var erros = query.Validar(consulta);

        if (aceito)
            Assert.Empty(erros);
        else
            Assert.Equal(nameof(FluxoCaixaQuery.ContaBancariaId), Assert.Single(erros).Key);
    }
}
