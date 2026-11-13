using System.Linq.Expressions;
using GestaoFinanceira.Services;
using System.Security.Claims;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Endpoints;

public static class LancamentosEndpoints
{
    private static readonly Expression<Func<Lancamento, LancamentoResponse>> Resposta = x =>
        new(x.Id, x.EmpresaId, x.Tipo, x.Descricao, x.ClienteId, x.FornecedorId, x.CategoriaId,
            x.CentroCustoId, x.DataEmissao, x.DataVencimento, x.Valor, x.Status, x.Observacoes, x.CriadoEm);

    public static void MapLancamentos(this WebApplication app)
    {
        var grupo = app.MapGroup("/lancamentos").RequireAuthorization();
        grupo.MapPost("", async (CriarLancamentoRequest request, HttpRequest http, ClaimsPrincipal principal,
            CadastroLancamentoService service, CancellationToken ct) =>
            http.Query.ContainsKey("empresaId")
                ? Results.BadRequest(new { mensagem = "Informe o escopo somente no corpo JSON." })
                : await service.CriarAsync(request, principal, ct)).WithName("CriarLancamento");
        grupo.MapGet("", Listar).WithName("ListarLancamentos");
        grupo.MapGet("/{id:long}", Buscar).WithName("BuscarLancamento");
    }

    private static async Task<IResult> Buscar(long id, long? empresaId, ClaimsPrincipal principal,
        GestaoFinanceiraContext db, CancellationToken ct)
    {
        if (empresaId <= 0) return Results.BadRequest(new { mensagem = "empresaId deve ser positivo." });
        var escopo = await EscopoFinanceiro.ResolverAsync(principal, empresaId, db, ct);
        if (escopo.Erro is not null) return escopo.Erro;
        var item = await db.Lancamentos.AsNoTracking().Where(x => x.EmpresaId == escopo.EmpresaId)
            .Where(x => x.Id == id).Select(Resposta).SingleOrDefaultAsync(ct);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> Listar([AsParameters] LancamentosQuery filtro,
        ClaimsPrincipal principal, GestaoFinanceiraContext db, CancellationToken ct)
    {
        var erros = filtro.Validar();
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var escopo = await EscopoFinanceiro.ResolverAsync(principal, filtro.EmpresaId, db, ct);
        if (escopo.Erro is not null) return escopo.Erro;
        var q = db.Lancamentos.AsNoTracking().Where(x => x.EmpresaId == escopo.EmpresaId);
        if (filtro.Tipo is not null) q = q.Where(x => x.Tipo == filtro.Tipo);
        if (filtro.Status is not null) q = q.Where(x => x.Status == filtro.Status);
        if (filtro.CategoriaId.HasValue) q = q.Where(x => x.CategoriaId == filtro.CategoriaId);
        if (filtro.ClienteId.HasValue) q = q.Where(x => x.ClienteId == filtro.ClienteId);
        if (filtro.FornecedorId.HasValue) q = q.Where(x => x.FornecedorId == filtro.FornecedorId);
        if (filtro.CentroCustoId.HasValue) q = q.Where(x => x.CentroCustoId == filtro.CentroCustoId);
        if (filtro.VencimentoInicio.HasValue)
        {
            var inicio = filtro.VencimentoInicio.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.DataVencimento >= inicio);
        }
        if (filtro.VencimentoFim.HasValue)
        {
            var fim = filtro.VencimentoFim.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.DataVencimento <= fim);
        }
        var total = await q.LongCountAsync(ct);
        var desc = filtro.Direcao == "desc";
        var ordenada = filtro.OrdenarPor switch
        {
            "valor" => desc ? q.OrderByDescending(x => x.Valor) : q.OrderBy(x => x.Valor),
            "dataEmissao" => desc ? q.OrderByDescending(x => x.DataEmissao) : q.OrderBy(x => x.DataEmissao),
            "id" => desc ? q.OrderByDescending(x => x.Id) : q.OrderBy(x => x.Id),
            _ => desc ? q.OrderByDescending(x => x.DataVencimento) : q.OrderBy(x => x.DataVencimento)
        };
        var itens = await ordenada.ThenBy(x => x.Id).Skip((int)(((long)filtro.Pagina - 1) * filtro.TamanhoPagina))
            .Take(filtro.TamanhoPagina).Select(Resposta).ToListAsync(ct);
        return Results.Ok(new PaginaLancamentos(itens, total, filtro.Pagina, filtro.TamanhoPagina,
            (total + filtro.TamanhoPagina - 1) / filtro.TamanhoPagina));
    }
}
