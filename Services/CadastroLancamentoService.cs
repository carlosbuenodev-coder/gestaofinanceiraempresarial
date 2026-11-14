using System.Security.Claims;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Services;

public sealed class CadastroLancamentoService(GestaoFinanceiraContext db, IHostEnvironment environment)
{
    public async Task<IResult> CriarAsync(CriarLancamentoRequest request, ClaimsPrincipal principal, CancellationToken ct)
    {
        var testing = environment.IsEnvironment("Testing");
        if (!testing && !environment.IsDevelopment())
            return Results.Problem("Cadastro financeiro permitido apenas em Development e Testing.", statusCode: 403);
        var perfil = principal.FindFirstValue(ClaimTypes.Role);
        if (perfil is not ("ADMIN" or "FINANCEIRO" or "ADMIN_GERAL")) return Results.Forbid();
        if (perfil != "ADMIN_GERAL" && request.EmpresaId.HasValue)
            return Results.BadRequest(new { mensagem = "Usuário empresarial não pode informar empresaId." });
        var erros = request.Validar();
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        await DatabaseSafety.VerifyAsync(db.Database.GetConnectionString()!, testing);
        await db.Database.OpenConnectionAsync(ct);
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), testing);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var escopo = await EscopoFinanceiro.ResolverAsync(principal, request.EmpresaId, db, ct);
        if (escopo.Erro is not null) return escopo.Erro;
        var empresa = escopo.EmpresaId;
        await ReferenciasLancamento.ValidarAsync(db, request, empresa, erros, ct);
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var item = new Lancamento
        {
            EmpresaId = empresa, Tipo = request.Tipo!, Descricao = request.Descricao!.Trim(),
            CategoriaId = request.CategoriaId!.Value, ClienteId = request.ClienteId,
            FornecedorId = request.FornecedorId, CentroCustoId = request.CentroCustoId,
            DataEmissao = request.DataEmissao!.Value.ToDateTime(TimeOnly.MinValue),
            DataVencimento = request.DataVencimento!.Value.ToDateTime(TimeOnly.MinValue),
            Valor = request.Valor!.Value, Observacoes = request.Observacoes, Status = "ABERTO"
        };
        try
        {
            db.Lancamentos.Add(item);
            await db.SaveChangesAsync(ct);
            var autorId = long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")!);
            var autorTipo = perfil == "ADMIN_GERAL" ? "ADMIN_GERAL" : "EMPRESA";
            db.Set<Auditoria>().Add(new Auditoria
            {
                EmpresaId = empresa, UsuarioId = autorTipo == "EMPRESA" ? autorId : null,
                Entidade = "lancamentos", RegistroId = item.Id, Acao = "CRIAR", CriadoEm = DateTime.UtcNow,
                Detalhes = JsonSerializer.Serialize(new { autorId, autorTipo, perfil })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Results.Problem("Não foi possível registrar lançamento e auditoria. Nenhuma gravação foi confirmada.", statusCode: 500);
        }
        var response = new LancamentoResponse(item.Id, item.EmpresaId, item.Tipo, item.Descricao,
            item.ClienteId, item.FornecedorId, item.CategoriaId, item.CentroCustoId, item.DataEmissao,
            item.DataVencimento, item.Valor, item.Status, item.Observacoes, item.CriadoEm);
        var location = $"/lancamentos/{item.Id}" + (perfil == "ADMIN_GERAL" ? $"?empresaId={empresa}" : "");
        return Results.Created(location, response);
    }
}
