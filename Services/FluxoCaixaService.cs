using System.Security.Claims;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Services;

public sealed class FluxoCaixaService(GestaoFinanceiraContext db)
{
    public async Task<IResult> ConsultarAsync(string consulta, FluxoCaixaQuery q, ClaimsPrincipal principal, CancellationToken ct)
    {
        var (empresa, erro) = await EscopoFinanceiro.ResolverAsync(principal, q.EmpresaId, db, ct);
        if (erro is not null) return erro;
        var erros = q.Validar(consulta);
        async Task Referencia<T>(long? id, string campo) where T : class, IReferenciaFinanceira
        {
            if (id.HasValue && !await db.Set<T>().AsNoTracking().AnyAsync(x => x.Id == id && x.EmpresaId == empresa, ct))
                erros[campo] = ["Referência inválida para a empresa."];
        }
        await Referencia<ContaBancariaFinanceira>(q.ContaBancariaId, nameof(q.ContaBancariaId));
        await Referencia<CategoriaFinanceira>(q.CategoriaId, nameof(q.CategoriaId));
        await Referencia<CentroCustoFinanceiro>(q.CentroCustoId, nameof(q.CentroCustoId));
        if (q.CategoriaId.HasValue && q.Tipo is not null && !await db.Set<CategoriaFinanceira>().AsNoTracking()
            .AnyAsync(x => x.Id == q.CategoriaId && x.EmpresaId == empresa && x.Tipo == q.Tipo, ct))
            erros[nameof(q.CategoriaId)] = ["Categoria incompatível com o tipo."];
        if (erros.Count > 0) return Results.ValidationProblem(erros);

        // One consistent read snapshot for counts, pages and dashboard aggregates. Never saves changes.
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var inicio = q.DataInicial!.Value.ToDateTime(TimeOnly.MinValue);
        var fim = q.DataFinal!.Value.ToDateTime(TimeOnly.MinValue);
        var lancamentos = db.Lancamentos.AsNoTracking().Where(x => x.EmpresaId == empresa);
        if (q.Tipo is not null) lancamentos = lancamentos.Where(x => x.Tipo == q.Tipo);
        if (q.CategoriaId.HasValue) lancamentos = lancamentos.Where(x => x.CategoriaId == q.CategoriaId);
        if (q.CentroCustoId.HasValue) lancamentos = lancamentos.Where(x => x.CentroCustoId == q.CentroCustoId);
        var validas = db.Set<BaixaFinanceira>().AsNoTracking()
            .Where(b => !db.Set<EstornoBaixa>().Any(e => e.BaixaId == b.Id));
        var saldos = lancamentos.Where(l => l.Status != "CANCELADO" && l.DataVencimento >= inicio && l.DataVencimento <= fim)
            .Select(l => new { L = l, Saldo = l.Valor - (validas.Where(b => b.LancamentoId == l.Id).Sum(b => (decimal?)b.Valor) ?? 0m) });
        if (consulta is not ("realizado" or "por-conta") && await saldos.AnyAsync(x => x.Saldo < 0, ct))
            return Results.Conflict(new { mensagem = "Principal baixado excede o valor original. Consulta prevista interrompida; nenhum dado foi alterado." });
        var previsto = saldos.Where(x => x.Saldo > 0)
            .Select(x => new Linha { LancamentoId = x.L.Id, Data = x.L.DataVencimento, Tipo = x.L.Tipo,
                Descricao = x.L.Descricao, CategoriaId = x.L.CategoriaId, CentroCustoId = x.L.CentroCustoId,
                Principal = x.Saldo, Previsto = true, BaixaId = null, ContaBancariaId = null, Juros = 0m, Multa = 0m, Desconto = 0m });
        // Historical effective payments remain realized even if a legacy launch was later cancelled.
        var realizado = from b in validas
                        join l in lancamentos on b.LancamentoId equals l.Id
                        join conta in db.Set<ContaBancariaFinanceira>() on b.ContaBancariaId equals conta.Id
                        where conta.EmpresaId == empresa && b.DataPagamento >= inicio && b.DataPagamento <= fim
                            && (!q.ContaBancariaId.HasValue || b.ContaBancariaId == q.ContaBancariaId)
                        select new Linha { LancamentoId = l.Id, BaixaId = b.Id, ContaBancariaId = b.ContaBancariaId,
                            Data = b.DataPagamento, Tipo = l.Tipo, Descricao = l.Descricao, CategoriaId = l.CategoriaId,
                            CentroCustoId = l.CentroCustoId, Principal = b.Valor, Juros = b.Juros, Multa = b.Multa, Desconto = b.Desconto,
                            Previsto = false };
        if (consulta is "previsto" or "realizado")
        {
            var fonte = consulta == "previsto" ? previsto : realizado;
            var total = await fonte.CountAsync(ct);
            var itens = await fonte.OrderBy(x => x.Data).ThenBy(x => x.LancamentoId).ThenBy(x => x.BaixaId)
                .Skip((q.Pagina - 1) * q.TamanhoPagina).Take(q.TamanhoPagina)
                .Select(x => new FluxoMovimentoResponse(x.LancamentoId, x.BaixaId, x.ContaBancariaId, x.Tipo,
                    x.Descricao, x.CategoriaId, x.CentroCustoId, x.Data, x.Principal, x.Juros, x.Multa, x.Desconto)).ToListAsync(ct);
            return Results.Ok(new FluxoPagina<FluxoMovimentoResponse>(empresa, q.Pagina, q.TamanhoPagina, total, itens));
        }
        async Task<FluxoPagina<FluxoContaResponse>> Contas()
        {
            var grupos = realizado.GroupBy(x => x.ContaBancariaId).Select(g => new {
                Id = g.Key!.Value, Receitas = g.Sum(x => x.Tipo == "RECEITA" ? x.Principal : 0m),
                Despesas = g.Sum(x => x.Tipo == "DESPESA" ? x.Principal : 0m) });
            var total = await grupos.CountAsync(ct);
            var itens = await grupos.OrderBy(x => x.Id).Skip((q.Pagina - 1) * q.TamanhoPagina).Take(q.TamanhoPagina)
                .Select(x => new FluxoContaResponse(x.Id, x.Receitas, x.Despesas, x.Receitas - x.Despesas, null)).ToListAsync(ct);
            return new(empresa, q.Pagina, q.TamanhoPagina, total, itens);
        }
        if (consulta == "por-conta") return Results.Ok(await Contas());
        // Normalize before UNION ALL; only numeric aggregates are returned to memory.
        var previstoNumerico = previsto.Select(x => new { x.LancamentoId, x.Data, x.Tipo, x.Principal, Previsto = true });
        var realizadoNumerico = realizado.Select(x => new { x.LancamentoId, x.Data, x.Tipo, x.Principal, Previsto = false });
        var movimentos = previstoNumerico.Concat(realizadoNumerico);
        async Task<FluxoPagina<FluxoPeriodoResponse>> Periodos()
        {
            var grupos = movimentos.GroupBy(x => x.Data).Select(g => new {
                Data = g.Key,
                RP = g.Sum(x => x.Previsto && x.Tipo == "RECEITA" ? x.Principal : 0m),
                DP = g.Sum(x => x.Previsto && x.Tipo == "DESPESA" ? x.Principal : 0m),
                RR = g.Sum(x => !x.Previsto && x.Tipo == "RECEITA" ? x.Principal : 0m),
                DR = g.Sum(x => !x.Previsto && x.Tipo == "DESPESA" ? x.Principal : 0m) });
            var total = await grupos.CountAsync(ct);
            var itens = await grupos.OrderBy(x => x.Data).Skip((q.Pagina - 1) * q.TamanhoPagina).Take(q.TamanhoPagina)
                .Select(x => new FluxoPeriodoResponse(x.Data, x.RP, x.DP, x.RR, x.DR, x.RP - x.DP, x.RR - x.DR)).ToListAsync(ct);
            return new(empresa, q.Pagina, q.TamanhoPagina, total, itens);
        }
        if (consulta == "por-periodo") return Results.Ok(await Periodos());
        var totais = await movimentos.GroupBy(x => 1).Select(g => new {
            RP = g.Sum(x => x.Previsto && x.Tipo == "RECEITA" ? x.Principal : 0m),
            DP = g.Sum(x => x.Previsto && x.Tipo == "DESPESA" ? x.Principal : 0m),
            RR = g.Sum(x => !x.Previsto && x.Tipo == "RECEITA" ? x.Principal : 0m),
            DR = g.Sum(x => !x.Previsto && x.Tipo == "DESPESA" ? x.Principal : 0m),
            Lancamentos = g.Select(x => x.LancamentoId).Distinct().Count(), Baixas = g.Count(x => !x.Previsto)
        }).SingleOrDefaultAsync(ct);
        var resumo = new FluxoResumoResponse(empresa, q.DataInicial.Value, q.DataFinal.Value,
            totais?.RP ?? 0m, totais?.DP ?? 0m, totais?.RR ?? 0m, totais?.DR ?? 0m,
            (totais?.RP ?? 0m) - (totais?.DP ?? 0m), (totais?.RR ?? 0m) - (totais?.DR ?? 0m),
            (totais?.RP ?? 0m) + (totais?.DP ?? 0m), totais?.Lancamentos ?? 0, totais?.Baixas ?? 0);
        if (consulta == "dashboard") return Results.Ok(new FluxoDashboardResponse(resumo, await Periodos(), await Contas()));
        return Results.Ok(resumo);
    }

    private sealed class Linha
    {
        public long LancamentoId { get; init; }
        public long? BaixaId { get; init; }
        public long? ContaBancariaId { get; init; }
        public DateTime Data { get; init; }
        public string Tipo { get; init; } = "";
        public string Descricao { get; init; } = "";
        public long CategoriaId { get; init; }
        public long? CentroCustoId { get; init; }
        public decimal Principal { get; init; }
        public decimal Juros { get; init; }
        public decimal Multa { get; init; }
        public decimal Desconto { get; init; }
        public bool Previsto { get; init; }
    }
}
