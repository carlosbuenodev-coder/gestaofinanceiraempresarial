using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GestaoFinanceira.Security;

public static class FluxoCaixaTestingScenario
{
    public static async Task RunAsync(GestaoFinanceiraContext db, HttpClient http, long a, long b,
        long adminId, string admin, string financeiro, string consulta, string adminB, string geral)
    {
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        var checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Fluxo de caixa: " + label);
            checks++;
        }
        async Task<long> Insert(string sql, params object[] values)
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = sql;
            for (var i = 0; i < values.Length; i++) { var p = cmd.CreateParameter(); p.ParameterName = "@p" + i; p.Value = values[i]; cmd.Parameters.Add(p); }
            await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear(); cmd.CommandText = "SELECT LAST_INSERT_ID()";
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        var cat = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,'Fluxo ficticio receita','RECEITA')", a);
        var desp = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,'Fluxo ficticio despesa','DESPESA')", a);
        var catB = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,'Fluxo ficticio receita','RECEITA')", b);
        var centro = await Insert("INSERT INTO centros_custo (empresa_id,nome) VALUES (@p0,'Fluxo ficticio')", a);
        var centroB = await Insert("INSERT INTO centros_custo (empresa_id,nome) VALUES (@p0,'Fluxo ficticio')", b);
        var conta = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao) VALUES (@p0,'Fluxo ficticio 1')", a);
        var conta2 = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao,ativo) VALUES (@p0,'Fluxo ficticio 2',0)", a);
        var contaB = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao) VALUES (@p0,'Fluxo ficticio B')", b);
        async Task<long> Lanc(decimal valor, string tipo = "RECEITA", string status = "ABERTO", string data = "2030-01-20", bool outra = false)
        {
            var l = new Lancamento { EmpresaId = outra ? b : a, CategoriaId = outra ? catB : tipo == "RECEITA" ? cat : desp,
                CentroCustoId = outra ? centroB : centro, Descricao = "Fluxo ficticio", Tipo = tipo, Status = status,
                Valor = valor, DataEmissao = new DateTime(2029, 12, 1), DataVencimento = DateTime.Parse(data, System.Globalization.CultureInfo.InvariantCulture) };
            db.Lancamentos.Add(l); await db.SaveChangesAsync(); return l.Id;
        }
        async Task Baixa(long id, decimal valor, string data, long? banco = null, bool estorno = false, bool encargos = false)
        {
            var baixa = await Insert("INSERT INTO baixas (lancamento_id,conta_bancaria_id,data_pagamento,valor,juros,multa,desconto) VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                id, banco ?? conta, data, valor, encargos ? 3m : 0m, encargos ? 2m : 0m, encargos ? 1m : 0m);
            if (estorno) await Insert("INSERT INTO estornos_baixas (baixa_id,motivo,criado_em) VALUES (@p0,'Fixture ficticia',UTC_TIMESTAMP(6))", baixa);
        }
        var parcial = await Lanc(1000, status: "PARCIAL");
        await Baixa(parcial, 100, "2029-12-29"); await Baixa(parcial, 200, "2030-01-05", encargos: true);
        await Baixa(parcial, 50, "2030-01-06", estorno: true); await Baixa(parcial, 100, "2030-02-01");
        var saida = await Lanc(500, "DESPESA", "PARCIAL"); await Baixa(saida, 150, "2030-01-07");
        var quitado = await Lanc(200, status: "QUITADO"); await Baixa(quitado, 200, "2030-01-08", conta2);
        await Lanc(300, status: "CANCELADO");
        var canceladoLegado = await Lanc(90, status: "CANCELADO"); await Baixa(canceladoLegado, 40, "2030-01-09");
        var restaurado = await Lanc(80); await Baixa(restaurado, 80, "2030-01-10", estorno: true);
        await Lanc(60, "DESPESA");
        var fora = await Lanc(300, status: "PARCIAL", data: "2030-02-20"); await Baixa(fora, 30, "2030-01-11");
        var outro = await Lanc(10000, outra: true); await Baixa(outro, 1000, "2030-01-05", contaB);
        var antes = await AlteracaoLancamentosTestingScenario.Snapshot(db);
        var filtro = $"dataInicial=2030-01-01&dataFinal=2030-01-31&centroCustoId={centro}";
        string Path(string endpoint, string? extra = null) => "/fluxo-caixa/" + endpoint + "?" + filtro + (extra is null ? "" : "&" + extra);
        async Task<HttpResponseMessage> Send(string path, string? token)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, path);
            if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await http.SendAsync(req);
        }
        async Task<T> Get<T>(string endpoint, string token, string? extra = null)
        {
            using var response = await Send(Path(endpoint, extra), token);
            Check((int)response.StatusCode == 200, endpoint + " HTTP " + (int)response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
        async Task Denied(string path, string? token, int status)
        {
            using var response = await Send(path, token);
            Check((int)response.StatusCode == status, path + " esperado " + status + " recebido " + (int)response.StatusCode);
        }
        foreach (var (token, empresa) in new[] { (admin, (long?)null), (financeiro, null), (consulta, null), (geral, a) })
        {
            var extra = empresa.HasValue ? "empresaId=" + empresa : null;
            var r = await Get<FluxoResumoResponse>("resumo", token, extra);
            Check(r.EmpresaId == a && r.ReceitasPrevistas == 680 && r.DespesasPrevistas == 410 &&
                r.ReceitasRealizadas == 470 && r.DespesasRealizadas == 150 && r.SaldoPrevisto == 270 &&
                r.SaldoRealizado == 320 && r.ValoresPendentes == 1090 && r.QuantidadeLancamentos == 7 && r.QuantidadeBaixasValidas == 5,
                "totais, estornos, cancelamento, parcial e datas independentes");
            var p = await Get<FluxoPagina<FluxoMovimentoResponse>>("previsto", token, extra);
            Check(p.Total == 4 && p.Itens.Single(x => x.LancamentoId == parcial).Principal == 600 && p.Itens.All(x => x.ContaBancariaId is null && x.BaixaId is null), "pendentes por principal e sem conta inventada");
            var real = await Get<FluxoPagina<FluxoMovimentoResponse>>("realizado", token, extra);
            var legado = real.Itens.Single(x => x.LancamentoId == parcial);
            Check(real.Total == 5 && legado.Principal == 200 && legado.Juros == 3 && legado.Multa == 2 && legado.Desconto == 1, "encargos separados");
            var contas = await Get<FluxoPagina<FluxoContaResponse>>("por-conta", token, extra);
            Check(contas.Total == 2 && contas.Itens.All(x => x.SaldoBancarioAcumulado is null) &&
                contas.Itens.Single(x => x.ContaBancariaId == conta).SaldoRealizado == 120 &&
                contas.Itens.Single(x => x.ContaBancariaId == conta2).SaldoRealizado == 200, "contas inclusive inativa histórica");
            var periodos = await Get<FluxoPagina<FluxoPeriodoResponse>>("por-periodo", token, extra);
            Check(periodos.Total == 6 && periodos.Itens.Sum(x => x.SaldoPrevisto) == 270 && periodos.Itens.Sum(x => x.SaldoRealizado) == 320, "agrupamento diário");
            var dashboard = await Get<FluxoDashboardResponse>("dashboard", token, extra);
            Check(dashboard.Resumo == r && dashboard.PorPeriodo.Itens.SequenceEqual(periodos.Itens) && dashboard.PorConta.Itens.SequenceEqual(contas.Itens), "dashboard consistente");
        }
        var categoria = await Get<FluxoResumoResponse>("resumo", admin, $"categoriaId={cat}&tipo=RECEITA");
        Check(categoria.ReceitasPrevistas == 680 && categoria.DespesasPrevistas == 0 && categoria.ReceitasRealizadas == 470, "filtros categoria/tipo");
        var banco = await Get<FluxoPagina<FluxoMovimentoResponse>>("realizado", admin, $"contaBancariaId={conta2}");
        Check(banco.Total == 1 && banco.Itens.Single().Principal == 200, "filtro conta");
        var pagina1 = await Get<FluxoPagina<FluxoMovimentoResponse>>("previsto", admin, "tamanhoPagina=2");
        var pagina2 = await Get<FluxoPagina<FluxoMovimentoResponse>>("previsto", admin, "tamanhoPagina=2&pagina=2");
        Check(pagina1.Total == 4 && pagina1.Itens.Count == 2 && pagina2.Itens.Count == 2 &&
            !pagina1.Itens.Select(x => x.LancamentoId).Intersect(pagina2.Itens.Select(x => x.LancamentoId)).Any(), "paginação estável com desempate");
        var vazia = await Get<FluxoPagina<FluxoMovimentoResponse>>("previsto", admin, "pagina=99");
        Check(vazia.Total == 4 && vazia.Itens.Count == 0, "página fora dos resultados");
        using (var empty = await Send("/fluxo-caixa/resumo?dataInicial=2099-01-01&dataFinal=2099-01-31", admin))
        {
            var r = await empty.Content.ReadFromJsonAsync<FluxoResumoResponse>();
            Check(empty.IsSuccessStatusCode && r is { ValoresPendentes: 0, QuantidadeLancamentos: 0, QuantidadeBaixasValidas: 0 }, "período vazio");
        }
        var expired = new JwtSecurityToken(issuer: "GestaoFinanceiraAPI.Testing", audience: "GestaoFinanceiraAPI.Testing",
            claims: [new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Role, "ADMIN"), new Claim("empresa_id", a.ToString()), new Claim("tipo_usuario", "EMPRESA")],
            notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(Environment.GetEnvironmentVariable("Testing__JwtKey")!)), SecurityAlgorithms.HmacSha256));
        foreach (var endpoint in new[] { "resumo", "previsto", "realizado", "por-conta", "por-periodo", "dashboard" })
        {
            await Denied(Path(endpoint), null, 401);
            await Denied(Path(endpoint), "invalid.jwt.token", 401);
            await Denied(Path(endpoint), new JwtSecurityTokenHandler().WriteToken(expired), 401);
            await Denied(Path(endpoint), geral, 400);
            await Denied(Path(endpoint, $"empresaId={b}"), admin, 403);
            await Denied(Path(endpoint), adminB, 400); // foreign center is indistinguishable from missing
            await Denied(Path(endpoint, $"empresaId={b}"), geral, 400);
            foreach (var extra in new[] { "pagina=0", "tamanhoPagina=0", "tamanhoPagina=101", "pagina=2147483647&tamanhoPagina=100", "pagina=abc",
                "tipo=OUTRO", "categoriaId=0", $"categoriaId={catB}", $"categoriaId={long.MaxValue}", $"centroCustoId={centroB}", $"contaBancariaId={contaB}",
                $"categoriaId={cat}&tipo=DESPESA" })
                await Denied(Path(endpoint, extra), admin, 400);
            await Denied($"/fluxo-caixa/{endpoint}?dataInicial=2030-02-01&dataFinal=2030-01-01", admin, 400);
            await Denied($"/fluxo-caixa/{endpoint}?dataInicial=2030-02-30&dataFinal=2030-03-01", admin, 400);
            await Denied($"/fluxo-caixa/{endpoint}", admin, 400);
        }
        foreach (var endpoint in new[] { "resumo", "previsto", "por-periodo", "dashboard" })
            await Denied(Path(endpoint, $"contaBancariaId={conta}"), admin, 400);
        var user = await db.Usuarios.SingleAsync(x => x.Id == adminId);
        user.Ativo = false; await db.SaveChangesAsync();
        try { foreach (var e in new[] { "resumo", "previsto", "realizado", "por-conta", "por-periodo", "dashboard" }) await Denied(Path(e), admin, 403); }
        finally { user.Ativo = true; await db.SaveChangesAsync(); }
        var company = await db.Empresas.SingleAsync(x => x.Id == a);
        company.Ativo = false; await db.SaveChangesAsync();
        try { foreach (var e in new[] { "resumo", "previsto", "realizado", "por-conta", "por-periodo", "dashboard" }) await Denied(Path(e), admin, 403); }
        finally { company.Ativo = true; await db.SaveChangesAsync(); }
        Check(antes == await AlteracaoLancamentosTestingScenario.Snapshot(db), "todas as consultas preservaram dados financeiros e auditoria");
        var inconsistente = await Lanc(10);
        await Baixa(inconsistente, 11, "2030-01-05");
        var snapshotInconsistente = await AlteracaoLancamentosTestingScenario.Snapshot(db);
        foreach (var e in new[] { "resumo", "previsto", "por-periodo", "dashboard" }) await Denied(Path(e), admin, 409);
        Check(snapshotInconsistente == await AlteracaoLancamentosTestingScenario.Snapshot(db), "inconsistência não foi corrigida automaticamente");
        Console.WriteLine($"[OK] Fluxo de caixa: {checks} verificações aprovadas; zero reprovadas.");
    }
}
