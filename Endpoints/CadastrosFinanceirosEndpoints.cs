using System.Security.Claims;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Endpoints;

public static class CadastrosFinanceirosEndpoints
{
    public static void MapCadastrosFinanceiros(this WebApplication app)
    {
        app.MapGet("/cadastros-financeiros", Listar)
            .RequireAuthorization("UsuarioEmpresa")
            .WithName("ListarCadastrosFinanceiros");
    }

    private static async Task<IResult> Listar(long? empresaId, ClaimsPrincipal principal,
        GestaoFinanceiraContext db, HttpResponse response, CancellationToken ct)
    {
        var escopo = await EscopoFinanceiro.ResolverAsync(principal, empresaId, db, ct);
        if (escopo.Erro is not null) return escopo.Erro;
        var empresa = escopo.EmpresaId;

        var categorias = await db.Set<CategoriaFinanceira>().AsNoTracking()
            .Where(c => c.EmpresaId == empresa).OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.Nome, c.Tipo }).ToListAsync(ct);
        var clientes = await db.Set<ClienteFinanceiro>().AsNoTracking()
            .Where(c => c.EmpresaId == empresa).OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.Nome, Ativo = c.Ativo == true }).ToListAsync(ct);
        var fornecedores = await db.Set<FornecedorFinanceiro>().AsNoTracking()
            .Where(f => f.EmpresaId == empresa).OrderBy(f => f.Nome).ThenBy(f => f.Id)
            .Select(f => new { f.Id, f.Nome, Ativo = f.Ativo == true }).ToListAsync(ct);
        var centrosCusto = await db.Set<CentroCustoFinanceiro>().AsNoTracking()
            .Where(c => c.EmpresaId == empresa).OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.Nome }).ToListAsync(ct);

        response.Headers.CacheControl = "no-store";
        return Results.Ok(new { EmpresaId = empresa, Categorias = categorias, Clientes = clientes,
            Fornecedores = fornecedores, CentrosCusto = centrosCusto });
    }
}
