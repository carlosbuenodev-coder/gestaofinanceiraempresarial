namespace GestaoFinanceira.Models;

public sealed record LancamentosQuery(
    long? EmpresaId = null, string? Tipo = null, string? Status = null,
    long? CategoriaId = null, long? ClienteId = null, long? FornecedorId = null,
    long? CentroCustoId = null, DateOnly? VencimentoInicio = null, DateOnly? VencimentoFim = null,
    int Pagina = 1, int TamanhoPagina = 20, string OrdenarPor = "dataVencimento", string Direcao = "asc")
{
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();
        void Erro(string campo, string mensagem) => erros[campo] = [mensagem];
        if (Tipo is not (null or "RECEITA" or "DESPESA")) Erro(nameof(Tipo), "Tipo inválido.");
        if (Status is not (null or "ABERTO" or "PARCIAL" or "QUITADO" or "CANCELADO")) Erro(nameof(Status), "Status inválido.");
        foreach (var (campo, valor) in new[] { (nameof(EmpresaId), EmpresaId), (nameof(CategoriaId), CategoriaId),
            (nameof(ClienteId), ClienteId), (nameof(FornecedorId), FornecedorId), (nameof(CentroCustoId), CentroCustoId) })
            if (valor <= 0) Erro(campo, "O ID deve ser positivo.");
        if (VencimentoInicio > VencimentoFim) Erro(nameof(VencimentoFim), "Intervalo de vencimento invertido.");
        if (Pagina < 1 || TamanhoPagina is < 1 or > 100 ||
            ((long)Pagina - 1) * TamanhoPagina > int.MaxValue)
            Erro(nameof(Pagina), "Página positiva, tamanho entre 1 e 100 e deslocamento até Int32.MaxValue são obrigatórios.");
        if (OrdenarPor is not ("id" or "dataVencimento" or "dataEmissao" or "valor")) Erro(nameof(OrdenarPor), "Ordenação inválida.");
        if (Direcao is not ("asc" or "desc")) Erro(nameof(Direcao), "Direção inválida.");
        return erros;
    }
}
