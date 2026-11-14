using System.Data;
using System.Security.Claims;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace GestaoFinanceira.Services;

public sealed class AlteracaoLancamentoService(GestaoFinanceiraContext db, IHostEnvironment environment)
{
    public Task<IResult> AtualizarAsync(long id, AtualizarLancamentoRequest request,
        ClaimsPrincipal principal, CancellationToken ct) => ExecutarAsync(id, request.EmpresaId, request, principal, ct);

    public Task<IResult> CancelarAsync(long id, CancelarLancamentoRequest request,
        ClaimsPrincipal principal, CancellationToken ct) => ExecutarAsync(id, request.EmpresaId, null, principal, ct);

    private async Task<IResult> ExecutarAsync(long id, long? empresaId, AtualizarLancamentoRequest? request,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        // Must run before database access, including when a Testing connection is injected.
        if (!environment.IsEnvironment("Testing"))
            return Results.Problem("Edição e cancelamento disponíveis somente em Testing.", statusCode: 403);
        var perfil = principal.FindFirstValue(ClaimTypes.Role);
        if (perfil is not ("ADMIN" or "FINANCEIRO" or "ADMIN_GERAL")) return Results.Forbid();
        if (empresaId <= 0 || (perfil != "ADMIN_GERAL" && empresaId.HasValue))
            return Results.BadRequest(new { mensagem = "Escopo inválido: somente ADMIN_GERAL informa empresaId no corpo JSON." });
        if (request is not null)
        {
            var erros = request.ComoCadastro("RECEITA").Validar();
            if (erros.Count > 0) return Results.ValidationProblem(erros);
        }
        await DatabaseSafety.VerifyAsync(db.Database.GetConnectionString()!, true);
        await db.Database.OpenConnectionAsync(ct);
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var escopo = await EscopoFinanceiro.ResolverAsync(principal, empresaId, db, ct);
            if (escopo.Erro is not null) return escopo.Erro;
            // Row lock serializes edits/cancellations. Scoped SQL prevents cross-company discovery.
            var rows = await db.Lancamentos.FromSqlInterpolated(
                $"SELECT * FROM lancamentos WHERE empresa_id = {escopo.EmpresaId} AND id = {id} FOR UPDATE")
                .ToListAsync(ct);
            var item = rows.SingleOrDefault();
            if (item is null) return Results.NotFound();
            if (item.Status != "ABERTO")
                return Results.Conflict(new { mensagem = "Somente lançamentos ABERTO podem ser editados ou cancelados. PARCIAL exige definição das regras de saldo; QUITADO e CANCELADO são bloqueados." });
            // Serializable isolation also protects the absence check against concurrent payments.
            if (await db.Set<BaixaFinanceira>().AsNoTracking().AnyAsync(b => b.LancamentoId == item.Id, ct))
                return Results.Conflict(new { mensagem = "Lançamento com baixas financeiras não pode ser editado ou cancelado. Estorno não disponível." });
            var anterior = Resposta(item);
            if (request is not null)
            {
                var erros = new Dictionary<string, string[]>();
                await ReferenciasLancamento.ValidarAsync(db, request.ComoCadastro(item.Tipo), item.EmpresaId, erros, ct);
                if (erros.Count > 0) return Results.ValidationProblem(erros);
                item.Descricao = request.Descricao!.Trim();
                item.CategoriaId = request.CategoriaId!.Value;
                item.ClienteId = request.ClienteId;
                item.FornecedorId = request.FornecedorId;
                item.CentroCustoId = request.CentroCustoId;
                item.DataEmissao = request.DataEmissao!.Value.ToDateTime(TimeOnly.MinValue);
                item.DataVencimento = request.DataVencimento!.Value.ToDateTime(TimeOnly.MinValue);
                item.Valor = request.Valor!.Value;
                item.Observacoes = request.Observacoes;
            }
            else item.Status = "CANCELADO";
            await db.SaveChangesAsync(ct);
            var posterior = Resposta(item);
            var autorId = long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")!);
            var autorTipo = perfil == "ADMIN_GERAL" ? "ADMIN_GERAL" : "EMPRESA";
            db.Set<Auditoria>().Add(new Auditoria
            {
                EmpresaId = item.EmpresaId, UsuarioId = autorTipo == "EMPRESA" ? autorId : null,
                Entidade = "lancamentos", RegistroId = item.Id, Acao = request is null ? "CANCELAR" : "ALTERAR",
                CriadoEm = DateTime.UtcNow,
                Detalhes = JsonSerializer.Serialize(new { autorId, autorTipo, perfil, anterior, posterior })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(posterior);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Results.Conflict(new { mensagem = "O lançamento foi alterado durante a operação. Consulte o estado atual." });
        }
        catch (Exception ex) when (ex is DbUpdateException or MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Results.Problem("Operação não confirmada; alteração e auditoria foram revertidas.", statusCode: 500);
        }
    }

    private static LancamentoResponse Resposta(Lancamento x) => new(x.Id, x.EmpresaId, x.Tipo,
        x.Descricao, x.ClienteId, x.FornecedorId, x.CategoriaId, x.CentroCustoId, x.DataEmissao,
        x.DataVencimento, x.Valor, x.Status, x.Observacoes, x.CriadoEm);
}
