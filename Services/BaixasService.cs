using System.Data;
using System.Security.Claims;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace GestaoFinanceira.Services;

public sealed class BaixasService(GestaoFinanceiraContext db, IHostEnvironment environment)
{
    private IResult? Bloqueio(ClaimsPrincipal principal, bool escrita)
    {
        if (!FinancialPaymentPolicy.AllowsPayments(environment.EnvironmentName))
            return Results.Problem("Módulo de baixas não habilitado neste ambiente.", statusCode: 403);
        if (escrita && principal.FindFirstValue(ClaimTypes.Role) is not ("ADMIN" or "FINANCEIRO" or "ADMIN_GERAL"))
            return Results.Forbid();
        return null;
    }

    private async Task AbrirAsync(CancellationToken ct)
    {
        var testing = environment.IsEnvironment("Testing");
        await DatabaseSafety.VerifyAsync(db.Database.GetConnectionString()!, testing);
        await db.Database.OpenConnectionAsync(ct);
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), testing);
    }

    private static string Status(decimal valor, decimal total) => total == 0 ? "ABERTO" : total == valor ? "QUITADO" : "PARCIAL";
    private static bool Inconsistente(Lancamento item, decimal total) => item.Valor <= 0 || total < 0 || total > item.Valor ||
        (item.Status == "CANCELADO" ? total != 0 : item.Status != Status(item.Valor, total));
    private static IResult Conflito(string mensagem) => Results.Conflict(new { mensagem });

    private async Task<decimal> TotalAsync(long lancamento, CancellationToken ct) => await db.Set<BaixaFinanceira>()
        .Where(b => b.LancamentoId == lancamento && !db.Set<EstornoBaixa>().Any(e => e.BaixaId == b.Id))
        .SumAsync(b => (decimal?)b.Valor, ct) ?? 0m;

    private IQueryable<BaixaFinanceira> BaixasDaEmpresa(long empresa) => db.Set<BaixaFinanceira>().AsNoTracking()
        .Where(b => db.Lancamentos.Any(l => l.Id == b.LancamentoId && l.EmpresaId == empresa));

    private IQueryable<BaixaResponse> Consultar(long empresa, long? baixaId = null, long? lancamentoId = null,
        int? deslocamento = null, int? tamanho = null)
    {
        var q = from b in BaixasDaEmpresa(empresa)
                join e in db.Set<EstornoBaixa>().AsNoTracking() on b.Id equals e.BaixaId into estornos
                from e in estornos.DefaultIfEmpty()
                where (!baixaId.HasValue || b.Id == baixaId) && (!lancamentoId.HasValue || b.LancamentoId == lancamentoId)
                select new { Baixa = b, Estorno = e };
        // Filter, count and paginate before constructing a DTO: MySQL cannot compose
        // further predicates over positional record constructors.
        q = q.OrderBy(x => x.Baixa.Id);
        if (deslocamento.HasValue) q = q.Skip(deslocamento.Value);
        if (tamanho.HasValue) q = q.Take(tamanho.Value);
        return q.Select(x => new BaixaResponse(x.Baixa.Id, empresa, x.Baixa.LancamentoId,
            x.Baixa.ContaBancariaId, x.Baixa.FormaPagamentoId, x.Baixa.DataPagamento, x.Baixa.Valor,
            x.Baixa.Juros, x.Baixa.Multa, x.Baixa.Desconto, x.Baixa.Observacoes, x.Estorno != null,
            x.Estorno == null ? null : (long?)x.Estorno.Id, x.Estorno == null ? null : x.Estorno.Motivo,
            x.Estorno == null ? null : (DateTime?)x.Estorno.CriadoEm));
    }

    public async Task<IResult> ConsultarAsync(string consulta, long id, long? empresaId, int pagina,
        int tamanhoPagina, ClaimsPrincipal principal, CancellationToken ct)
    {
        var bloqueio = Bloqueio(principal, false); if (bloqueio is not null) return bloqueio;
        if (empresaId <= 0 || pagina < 1 || tamanhoPagina is < 1 or > 100 || ((long)pagina - 1) * tamanhoPagina > int.MaxValue)
            return Results.BadRequest(new { mensagem = "IDs positivos, página positiva e tamanho entre 1 e 100 são obrigatórios." });
        await AbrirAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var escopo = await EscopoFinanceiro.ResolverAsync(principal, empresaId, db, ct);
        if (escopo.Erro is not null) return escopo.Erro;
        if (consulta == "baixa")
        {
            var baixa = await Consultar(escopo.EmpresaId, baixaId: id).SingleOrDefaultAsync(ct);
            return baixa is null ? Results.NotFound() : Results.Ok(baixa);
        }
        var item = await db.Lancamentos.AsNoTracking().SingleOrDefaultAsync(l => l.EmpresaId == escopo.EmpresaId && l.Id == id, ct);
        if (item is null) return Results.NotFound();
        if (consulta == "saldo")
        {
            var total = await TotalAsync(item.Id, ct);
            if (Inconsistente(item, total)) return Conflito("Situação financeira inconsistente; saldo e status exigem revisão, sem correção automática.");
            return Results.Ok(new SaldoLancamentoResponse(item.Id, item.EmpresaId, item.Valor, total, item.Valor - total, item.Status));
        }
        var count = await BaixasDaEmpresa(escopo.EmpresaId).LongCountAsync(b => b.LancamentoId == item.Id, ct);
        var itens = await Consultar(escopo.EmpresaId, lancamentoId: item.Id,
            deslocamento: (int)(((long)pagina - 1) * tamanhoPagina), tamanho: tamanhoPagina).ToListAsync(ct);
        return Results.Ok(new PaginaBaixas(itens, count, pagina, tamanhoPagina));
    }

    public Task<IResult> CriarAsync(long id, CriarBaixaRequest request, ClaimsPrincipal principal, CancellationToken ct)
        => MovimentarAsync(id, request, null, principal, ct);
    public Task<IResult> EstornarAsync(long id, EstornarBaixaRequest request, ClaimsPrincipal principal, CancellationToken ct)
        => MovimentarAsync(id, null, request, principal, ct);

    private async Task<IResult> MovimentarAsync(long id, CriarBaixaRequest? request, EstornarBaixaRequest? estorno,
        ClaimsPrincipal principal, CancellationToken ct)
    {
        var bloqueio = Bloqueio(principal, true); if (bloqueio is not null) return bloqueio;
        var empresaId = request?.EmpresaId ?? estorno?.EmpresaId;
        var perfil = principal.FindFirstValue(ClaimTypes.Role);
        if (perfil != "ADMIN_GERAL" && empresaId.HasValue)
            return Results.BadRequest(new { mensagem = "Usuário empresarial não pode informar empresaId." });
        var erros = request is not null ? request.Validar() : estorno!.Validar();
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        await AbrirAsync(ct);
        // Current reads plus a parent row lock serialize all payments and reversals for that entry.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var escopo = await EscopoFinanceiro.ResolverAsync(principal, empresaId, db, ct);
            if (escopo.Erro is not null) return escopo.Erro;
            long lancamentoId = id;
            if (estorno is not null)
            {
                var vinculo = await BaixasDaEmpresa(escopo.EmpresaId).Where(b => b.Id == id).Select(b => (long?)b.LancamentoId).SingleOrDefaultAsync(ct);
                if (!vinculo.HasValue) return Results.NotFound();
                lancamentoId = vinculo.Value;
            }
            var rows = await db.Lancamentos.FromSqlInterpolated(
                $"SELECT * FROM lancamentos WHERE empresa_id={escopo.EmpresaId} AND id={lancamentoId} FOR UPDATE").ToListAsync(ct);
            var item = rows.SingleOrDefault();
            if (item is null) return Results.NotFound();
            if (item.Status == "CANCELADO") return Conflito("Lançamento cancelado não aceita baixa ou estorno.");
            var total = await TotalAsync(item.Id, ct);
            if (Inconsistente(item, total)) return Conflito("Situação financeira inconsistente; operação bloqueada sem alterar saldo ou status.");
            var anterior = new SaldoLancamentoResponse(item.Id, item.EmpresaId, item.Valor, total, item.Valor - total, item.Status);
            BaixaFinanceira baixa;
            BaixaResponse? baixaAnterior = null;
            EstornoBaixa? reversao = null;
            if (request is not null)
            {
                if (item.Status is not ("ABERTO" or "PARCIAL")) return Conflito("Somente lançamentos ABERTO ou PARCIAL aceitam baixas.");
                if (request.Valor!.Value > item.Valor - total) return Conflito("Valor da baixa superior ao saldo restante.");
                if (!await db.Set<ContaBancariaFinanceira>().AsNoTracking().AnyAsync(c => c.Id == request.ContaBancariaId && c.EmpresaId == item.EmpresaId && c.Ativo == true, ct))
                    erros[nameof(request.ContaBancariaId)] = ["Conta bancária inexistente, inativa ou fora da empresa."];
                if (request.FormaPagamentoId.HasValue && !await db.Set<FormaPagamentoFinanceira>().AnyAsync(f => f.Id == request.FormaPagamentoId, ct))
                    erros[nameof(request.FormaPagamentoId)] = ["Forma de pagamento inválida."];
                if (erros.Count > 0) return Results.ValidationProblem(erros);
                baixa = new BaixaFinanceira { LancamentoId = item.Id, ContaBancariaId = request.ContaBancariaId!.Value,
                    FormaPagamentoId = request.FormaPagamentoId, DataPagamento = request.DataPagamento!.Value.ToDateTime(TimeOnly.MinValue),
                    Valor = request.Valor.Value, Observacoes = request.Observacoes };
                total += baixa.Valor;
                db.Set<BaixaFinanceira>().Add(baixa);
            }
            else
            {
                baixaAnterior = await Consultar(item.EmpresaId, baixaId: id, lancamentoId: item.Id).SingleAsync(ct);
                if (baixaAnterior.Estornada) return Conflito("Baixa já estornada.");
                baixa = await db.Set<BaixaFinanceira>().AsNoTracking().SingleAsync(b => b.Id == id && b.LancamentoId == item.Id, ct);
                reversao = new EstornoBaixa { BaixaId = baixa.Id, Motivo = estorno!.Motivo!.Trim(), CriadoEm = DateTime.UtcNow };
                db.Set<EstornoBaixa>().Add(reversao);
                total -= baixa.Valor;
            }
            item.Status = Status(item.Valor, total);
            db.OperacaoBaixa = true;
            await db.SaveChangesAsync(ct);
            var baixaPosterior = await Consultar(item.EmpresaId, baixaId: baixa.Id).SingleAsync(ct);
            var posterior = new SaldoLancamentoResponse(item.Id, item.EmpresaId, item.Valor, total, item.Valor - total, item.Status);
            var autorId = long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")!);
            void Auditar(string entidade, long registro, string acao, object? antes, object depois)
            {
                db.Set<Auditoria>().Add(new Auditoria { EmpresaId = item.EmpresaId, UsuarioId = perfil == "ADMIN_GERAL" ? null : autorId,
                    Entidade = entidade, RegistroId = registro, Acao = acao, CriadoEm = DateTime.UtcNow,
                    Detalhes = JsonSerializer.Serialize(new { autorId, autorTipo = perfil == "ADMIN_GERAL" ? "ADMIN_GERAL" : "EMPRESA", perfil, anterior = antes, posterior = depois }) });
            }
            Auditar("baixas", baixa.Id, request is not null ? "CRIAR_BAIXA" : "ESTORNAR_BAIXA", baixaAnterior, baixaPosterior);
            if (anterior.Status != posterior.Status) Auditar("lancamentos", item.Id, "ATUALIZAR_STATUS", anterior, posterior);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            var response = new MovimentoBaixaResponse(baixaPosterior, posterior);
            return request is null ? Results.Ok(response) : Results.Created($"/baixas/{baixa.Id}" +
                (perfil == "ADMIN_GERAL" ? $"?empresaId={item.EmpresaId}" : ""), response);
        }
        catch (Exception ex) when (ex is DbUpdateException or MySqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            for (Exception? atual = ex; atual is not null; atual = atual.InnerException)
                if (atual is DbUpdateConcurrencyException || atual is MySqlException { Number: 1062 or 1205 or 1213 })
                    return Conflito("Conflito concorrente; consulte o saldo e o histórico antes de reenviar.");
            return Results.Problem("Movimentação e auditoria não confirmadas; transação revertida. Consulte o estado antes de reenviar.", statusCode: 500);
        }
        finally { db.OperacaoBaixa = false; }
    }
}
