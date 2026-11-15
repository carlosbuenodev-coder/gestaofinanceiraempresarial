using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GestaoFinanceira.Security;

public static class BaixasTestingScenario
{
    public static async Task RunAsync(IServiceProvider services, GestaoFinanceiraContext db, HttpClient http,
        long a, long b, long adminId, long financeiroId, long geralId,
        string admin, string financeiro, string consulta, string adminB, string geral)
    {
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        var checks = 0;
        static void Check(bool condition, string label)
        {
            if (condition) return;
            Console.WriteLine("[FALHOU] Baixas: " + label);
            throw new InvalidOperationException("Falha no cenário de baixas.");
        }
        async Task<long> Insert(string sql, params object[] values)
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = sql;
            for (var i = 0; i < values.Length; i++) { var p = cmd.CreateParameter(); p.ParameterName = "@p" + i; p.Value = values[i]; cmd.Parameters.Add(p); }
            await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear(); cmd.CommandText = "SELECT LAST_INSERT_ID()";
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        var cat = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,'Fixture baixa receita','RECEITA')", a);
        var catB = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,'Fixture baixa receita','RECEITA')", b);
        var despesa = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,'Fixture baixa despesa','DESPESA')", a);
        var conta = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao) VALUES (@p0,'Conta fictícia baixa')", a);
        var contaB = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao) VALUES (@p0,'Conta fictícia outra empresa')", b);
        var inativa = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao,ativo) VALUES (@p0,'Conta fictícia inativa',0)", a);
        var forma = await Insert("INSERT INTO formas_pagamento (nome) VALUES (@p0)", "Baixas-" + Guid.NewGuid().ToString("N"));
        async Task<long> Novo(string tipo = "RECEITA", string status = "ABERTO", long? empresa = null)
        {
            var item = new Lancamento { EmpresaId = empresa ?? a, Tipo = tipo, Status = status, Descricao = "Lançamento fictício baixas",
                CategoriaId = empresa == b ? catB : tipo == "DESPESA" ? despesa : cat, Valor = 100m,
                DataEmissao = new DateTime(2026, 11, 1), DataVencimento = new DateTime(2026, 11, 30) };
            db.Lancamentos.Add(item); await db.SaveChangesAsync(); return item.Id;
        }
        var body = new CriarBaixaRequest(conta, forma, new DateOnly(2026, 11, 14), 30m, "Pagamento fictício");
        Task<string> Snapshot() => AlteracaoLancamentosTestingScenario.Snapshot(db);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? value = null)
        {
            using var req = new HttpRequestMessage(method, path);
            if (value is not null) req.Content = JsonContent.Create(value);
            if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await http.SendAsync(req);
        }
        async Task Denied(HttpMethod method, string path, string? token, object? value, int expected)
        {
            var before = await Snapshot();
            using var result = await Send(method, path, token, value);
            Check((int)result.StatusCode == expected, $"{method} {path}: esperado {expected}, recebido {(int)result.StatusCode}");
            Check(before == await Snapshot(), "operação negada alterou dados"); checks++;
        }
        async Task<T> Get<T>(string path, string token)
        {
            using var result = await Send(HttpMethod.Get, path, token);
            Check((int)result.StatusCode == 200, "consulta autorizada " + path);
            var value = await result.Content.ReadFromJsonAsync<T>();
            Check(value is not null, "DTO ausente"); checks++; return value!;
        }
        async Task Saldo(long id, string token, decimal total, string status, long? empresa = null)
        {
            var s = await Get<SaldoLancamentoResponse>($"/lancamentos/{id}/saldo" + (empresa.HasValue ? $"?empresaId={empresa}" : ""), token);
            Check(s.LancamentoId == id && s.EmpresaId == (empresa ?? a) && s.ValorOriginal == 100m && s.TotalBaixado == total &&
                s.SaldoRestante == 100m - total && s.Status == status, "saldo/status incorretos");
        }
        async Task<MovimentoBaixaResponse> Criar(long id, CriarBaixaRequest request, string token, long autor, string autorTipo,
            decimal total, string status, long? empresa = null)
        {
            var antes = await db.Lancamentos.AsNoTracking().SingleAsync(x => x.Id == id);
            var statusAudits = await db.Set<Auditoria>().CountAsync(x => x.RegistroId == id && x.Entidade == "lancamentos" && x.Acao == "ATUALIZAR_STATUS");
            using var result = await Send(HttpMethod.Post, $"/lancamentos/{id}/baixas", token, request);
            Check((int)result.StatusCode == 201, "baixa autorizada: " + (int)result.StatusCode);
            var movement = (await result.Content.ReadFromJsonAsync<MovimentoBaixaResponse>())!;
            var baixa = movement.Baixa;
            Check(baixa.Id > 0 && baixa.EmpresaId == (empresa ?? a) && baixa.LancamentoId == id && !baixa.Estornada &&
                baixa.Valor == request.Valor && baixa.Juros == 0 && baixa.Multa == 0 && baixa.Desconto == 0 &&
                baixa.ContaBancariaId == request.ContaBancariaId && baixa.FormaPagamentoId == request.FormaPagamentoId &&
                baixa.Observacoes == request.Observacoes && DateOnly.FromDateTime(baixa.DataPagamento) == request.DataPagamento, "baixa persistida incorreta");
            Check(movement.Saldo.TotalBaixado == total && movement.Saldo.SaldoRestante == 100 - total && movement.Saldo.Status == status, "resposta de saldo");
            var audit = await db.Set<Auditoria>().AsNoTracking().SingleAsync(x => x.Entidade == "baixas" && x.RegistroId == baixa.Id && x.Acao == "CRIAR_BAIXA");
            using var j = JsonDocument.Parse(audit.Detalhes);
            Check(audit.EmpresaId == (empresa ?? a) && audit.UsuarioId == (autorTipo == "EMPRESA" ? autor : (long?)null) && audit.CriadoEm is not null &&
                j.RootElement.GetProperty("autorId").GetInt64() == autor && j.RootElement.GetProperty("autorTipo").GetString() == autorTipo &&
                j.RootElement.GetProperty("anterior").ValueKind == JsonValueKind.Null && j.RootElement.GetProperty("posterior").Deserialize<BaixaResponse>() == baixa,
                "auditoria de criação");
            Check(await db.Set<Auditoria>().CountAsync(x => x.RegistroId == id && x.Entidade == "lancamentos" && x.Acao == "ATUALIZAR_STATUS") ==
                statusAudits + (antes.Status == status ? 0 : 1), "auditoria de status");
            var depois = await db.Lancamentos.AsNoTracking().SingleAsync(x => x.Id == id);
            Check(antes.Id == depois.Id && antes.EmpresaId == depois.EmpresaId && antes.Tipo == depois.Tipo && antes.Valor == depois.Valor &&
                antes.CriadoEm == depois.CriadoEm && antes.Descricao == depois.Descricao, "baixa alterou campos do lançamento");
            var fetched = await Get<BaixaResponse>(result.Headers.Location!.ToString(), token); Check(fetched == baixa, "Location/GET da baixa");
            checks++; return movement;
        }
        async Task Estornar(BaixaResponse baixa, string token, long autor, string autorTipo, decimal total, string status, long? empresa = null)
        {
            var original = await db.Set<BaixaFinanceira>().AsNoTracking().SingleAsync(x => x.Id == baixa.Id);
            var bytesAntes = JsonSerializer.Serialize(original);
            var motivo = "Estorno fictício controlado";
            using var result = await Send(HttpMethod.Post, $"/baixas/{baixa.Id}/estorno", token, new EstornarBaixaRequest(motivo, empresa));
            Check((int)result.StatusCode == 200, "estorno autorizado");
            var movement = (await result.Content.ReadFromJsonAsync<MovimentoBaixaResponse>())!;
            Check(movement.Baixa.Estornada && movement.Baixa.EstornoId > 0 && movement.Baixa.MotivoEstorno == motivo && movement.Baixa.EstornadaEm is not null &&
                movement.Saldo.TotalBaixado == total && movement.Saldo.SaldoRestante == 100 - total && movement.Saldo.Status == status, "recálculo de estorno");
            Check(bytesAntes == JsonSerializer.Serialize(await db.Set<BaixaFinanceira>().AsNoTracking().SingleAsync(x => x.Id == baixa.Id)), "estorno alterou baixa original");
            var audit = await db.Set<Auditoria>().AsNoTracking().SingleAsync(x => x.Entidade == "baixas" && x.RegistroId == baixa.Id && x.Acao == "ESTORNAR_BAIXA");
            using var j = JsonDocument.Parse(audit.Detalhes);
            Check(audit.EmpresaId == baixa.EmpresaId && audit.UsuarioId == (autorTipo == "EMPRESA" ? autor : (long?)null) &&
                j.RootElement.GetProperty("autorId").GetInt64() == autor && j.RootElement.GetProperty("autorTipo").GetString() == autorTipo &&
                j.RootElement.GetProperty("anterior").Deserialize<BaixaResponse>() == baixa &&
                j.RootElement.GetProperty("posterior").Deserialize<BaixaResponse>() == movement.Baixa, "auditoria de estorno");
            await Saldo(baixa.LancamentoId, token, total, status, empresa); checks++;
        }

        var id = await Novo();
        await Saldo(id, admin, 0, "ABERTO");
        var vazio = await Get<PaginaBaixas>($"/lancamentos/{id}/baixas", admin); Check(vazio.Total == 0 && vazio.Itens.Count == 0, "lista vazia");
        var p1 = await Criar(id, body, admin, adminId, "EMPRESA", 30, "PARCIAL");
        var p2 = await Criar(id, body with { Valor = 20, FormaPagamentoId = null, Observacoes = null }, financeiro, financeiroId, "EMPRESA", 50, "PARCIAL");
        var p3 = await Criar(id, body with { Valor = 50, EmpresaId = a }, geral, geralId, "ADMIN_GERAL", 100, "QUITADO");
        foreach (var token in new[] { admin, financeiro, consulta })
        {
            await Saldo(id, token, 100, "QUITADO");
            var page = await Get<PaginaBaixas>($"/lancamentos/{id}/baixas?pagina=2&tamanhoPagina=1", token);
            Check(page.Total == 3 && page.Pagina == 2 && page.TamanhoPagina == 1 && page.Itens.Single().Id == p2.Baixa.Id && page.Itens.All(x => x.EmpresaId == a), "paginação/empresa");
            Check(await Get<BaixaResponse>($"/baixas/{p1.Baixa.Id}", token) == p1.Baixa, "detalhe de baixa");
        }
        await Saldo(id, geral, 100, "QUITADO", a);
        var pgGeral = await Get<PaginaBaixas>($"/lancamentos/{id}/baixas?empresaId={a}", geral); Check(pgGeral.Total == 3, "lista geral");
        await Denied(HttpMethod.Post, $"/lancamentos/{id}/baixas", admin, body, 409);
        await Estornar(p1.Baixa, admin, adminId, "EMPRESA", 70, "PARCIAL");
        await Denied(HttpMethod.Post, $"/baixas/{p1.Baixa.Id}/estorno", admin, new EstornarBaixaRequest("Duplicado fictício"), 409);
        await Estornar(p2.Baixa, financeiro, financeiroId, "EMPRESA", 50, "PARCIAL");
        await Estornar(p3.Baixa, geral, geralId, "ADMIN_GERAL", 0, "ABERTO", a);
        var historico = await Get<PaginaBaixas>($"/lancamentos/{id}/baixas", consulta);
        Check(historico.Total == 3 && historico.Itens.All(x => x.Estornada), "histórico de estornos preservado");
        await Criar(id, body with { Valor = 100 }, admin, adminId, "EMPRESA", 100, "QUITADO");
        var despesaId = await Novo("DESPESA");
        var paidDespesa = await Criar(despesaId, body with { Valor = 100 }, financeiro, financeiroId, "EMPRESA", 100, "QUITADO");
        await Estornar(paidDespesa.Baixa, financeiro, financeiroId, "EMPRESA", 0, "ABERTO");
        var empresaB = await Novo(empresa: b);
        var paidB = await Criar(empresaB, body with { ContaBancariaId = contaB, EmpresaId = b, Valor = 100 }, geral, geralId, "ADMIN_GERAL", 100, "QUITADO", b);
        await Saldo(empresaB, adminB, 100, "QUITADO", b);
        await Estornar(paidB.Baixa, geral, geralId, "ADMIN_GERAL", 0, "ABERTO", b);

        var aberto = await Novo();
        var target = $"/lancamentos/{aberto}/baixas";
        foreach (var invalid in new[] { body with { Valor = 0 }, body with { Valor = -1 }, body with { Valor = 1.001m }, body with { Valor = null },
            body with { Valor = 10000000000000m }, body with { ContaBancariaId = null }, body with { ContaBancariaId = 0 },
            body with { ContaBancariaId = contaB }, body with { ContaBancariaId = inativa }, body with { ContaBancariaId = long.MaxValue },
            body with { FormaPagamentoId = 0 }, body with { FormaPagamentoId = long.MaxValue }, body with { DataPagamento = null },
            body with { DataPagamento = DateOnly.MinValue }, body with { Observacoes = new string('x', 256) }, body with { EmpresaId = a } })
            await Denied(HttpMethod.Post, target, admin, invalid, 400);
        await Denied(HttpMethod.Post, target, admin, body with { Valor = 100.01m }, 409);
        await Denied(HttpMethod.Post, target, admin, new { contaBancariaId = conta, dataPagamento = "2026-02-30", valor = 10 }, 400);
        foreach (var extra in new[] { "juros", "multa", "desconto", "status", "empresa", "estornada" })
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(body))!; payload[extra] = 1;
            await Denied(HttpMethod.Post, target, admin, payload, 400);
        }
        await Denied(HttpMethod.Post, target + $"?empresaId={a}", admin, body, 400);
        await Denied(HttpMethod.Post, target, geral, body, 400);
        await Denied(HttpMethod.Post, target, geral, body with { EmpresaId = b }, 404);
        await Denied(HttpMethod.Post, $"/lancamentos/{empresaB}/baixas", admin, body, 404);
        await Denied(HttpMethod.Post, "/lancamentos/9223372036854775807/baixas", admin, body, 404);
        await Denied(HttpMethod.Post, $"/lancamentos/{await Novo(status: "CANCELADO")}/baixas", admin, body, 409);
        await Denied(HttpMethod.Post, $"/lancamentos/{await Novo(status: "PARCIAL")}/baixas", admin, body, 409);
        foreach (var path in new[] { $"/lancamentos/{aberto}/saldo", target, $"/baixas/{p1.Baixa.Id}" })
        {
            await Denied(HttpMethod.Get, path, adminB, null, 404);
            await Denied(HttpMethod.Get, path, geral, null, 400);
            await Denied(HttpMethod.Get, path + $"?empresaId={b}", admin, null, 403);
            await Denied(HttpMethod.Get, path, null, null, 401);
        }
        foreach (var query in new[] { "pagina=0", "tamanhoPagina=0", "tamanhoPagina=101", "pagina=2147483647&tamanhoPagina=100", "pagina=abc" })
            await Denied(HttpMethod.Get, target + "?" + query, admin, null, 400);
        await Denied(HttpMethod.Get, "/baixas/9223372036854775807", admin, null, 404);
        await Denied(HttpMethod.Get, "/lancamentos/9223372036854775807/saldo", admin, null, 404);
        foreach (var motivo in new string?[] { null, " ", new string('x', 501) })
            await Denied(HttpMethod.Post, $"/baixas/{p1.Baixa.Id}/estorno", admin, new EstornarBaixaRequest(motivo), 400);
        await Denied(HttpMethod.Post, $"/baixas/{p1.Baixa.Id}/estorno", adminB, new EstornarBaixaRequest("Fictício"), 404);
        await Denied(HttpMethod.Post, $"/baixas/{p1.Baixa.Id}/estorno", geral, new EstornarBaixaRequest("Fictício"), 400);
        await Denied(HttpMethod.Post, $"/baixas/{p1.Baixa.Id}/estorno", geral, new EstornarBaixaRequest("Fictício", b), 404);
        await Denied(HttpMethod.Post, "/baixas/9223372036854775807/estorno", admin, new EstornarBaixaRequest("Fictício"), 404);
        var expired = new JwtSecurityToken(issuer: "GestaoFinanceiraAPI.Testing", audience: "GestaoFinanceiraAPI.Testing",
            claims: [new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Role, "ADMIN"), new Claim("empresa_id", a.ToString()), new Claim("tipo_usuario", "EMPRESA")],
            notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(Environment.GetEnvironmentVariable("Testing__JwtKey")!)), SecurityAlgorithms.HmacSha256));
        foreach (var token in new string?[] { consulta, null, "invalid.jwt.token", new JwtSecurityTokenHandler().WriteToken(expired) })
        {
            var code = token == consulta ? 403 : 401;
            await Denied(HttpMethod.Post, target, token, body, code);
            await Denied(HttpMethod.Post, $"/baixas/{p1.Baixa.Id}/estorno", token, new EstornarBaixaRequest("Fictício"), code);
        }
        // Legacy adjustments are disclosed but only principal reduces the balance, as authorized.
        var legado = await Novo(status: "PARCIAL");
        var legadoBaixa = await Insert("INSERT INTO baixas (lancamento_id,conta_bancaria_id,data_pagamento,valor,juros,multa,desconto) VALUES (@p0,@p1,'2026-11-14',10,1,2,0.50)", legado, conta);
        await Saldo(legado, admin, 10, "PARCIAL");
        var legacyDto = await Get<BaixaResponse>($"/baixas/{legadoBaixa}", admin);
        Check(legacyDto.Juros == 1 && legacyDto.Multa == 2 && legacyDto.Desconto == 0.50m, "ajustes legados não expostos");
        await Estornar(legacyDto, admin, adminId, "EMPRESA", 0, "ABERTO");
        var inconsistente = await Novo(status: "PARCIAL");
        await Insert("INSERT INTO baixas (lancamento_id,conta_bancaria_id,data_pagamento,valor) VALUES (@p0,@p1,'2026-11-14',101)", inconsistente, conta);
        await Denied(HttpMethod.Get, $"/lancamentos/{inconsistente}/saldo", admin, null, 409);
        await Denied(HttpMethod.Post, $"/lancamentos/{inconsistente}/baixas", admin, body, 409);
        // Financial data is sent before the injected audit failure; all persisted values must roll back.
        var failure = services.GetRequiredService<AuditFailureInterceptor>();
        var failures = failure.Failures; failure.Arm();
        await Denied(HttpMethod.Post, target, admin, body, 500); Check(failure.Failures == failures + 1, "rollback criação não exercitado");
        var reversivel = await Criar(aberto, body, admin, adminId, "EMPRESA", 30, "PARCIAL");
        failures = failure.Failures; failure.Arm();
        await Denied(HttpMethod.Post, $"/baixas/{reversivel.Baixa.Id}/estorno", admin, new EstornarBaixaRequest("Rollback fictício"), 500);
        Check(failure.Failures == failures + 1, "rollback estorno não exercitado");
        // Same lock protocol as edition/cancellation: payment history still blocks those operations.
        var update = new AtualizarLancamentoRequest("Bloqueado fictício", cat, DataEmissao: new DateOnly(2026, 11, 1), DataVencimento: new DateOnly(2026, 11, 30), Valor: 100);
        await Denied(HttpMethod.Put, $"/lancamentos/{aberto}", admin, update, 409);
        await Denied(HttpMethod.Post, $"/lancamentos/{aberto}/cancelamento", admin, new CancelarLancamentoRequest(), 409);
        await Estornar(reversivel.Baixa, admin, adminId, "EMPRESA", 0, "ABERTO");
        await Denied(HttpMethod.Put, $"/lancamentos/{aberto}", admin, update, 409);
        await Denied(HttpMethod.Post, $"/lancamentos/{aberto}/cancelamento", admin, new CancelarLancamentoRequest(), 409);
        foreach (var valor in new[] { 70m, 100m, 40m })
        {
            var corrida = await Novo();
            var responses = await Task.WhenAll(Send(HttpMethod.Post, $"/lancamentos/{corrida}/baixas", admin, body with { Valor = valor }),
                Send(HttpMethod.Post, $"/lancamentos/{corrida}/baixas", financeiro, body with { Valor = valor }));
            try
            {
                var expected = valor == 40 ? new[] { 201, 201 } : new[] { 201, 409 };
                Check(responses.Select(x => (int)x.StatusCode).Order().SequenceEqual(expected), "baixas concorrentes");
                await Saldo(corrida, admin, valor == 40 ? 80 : valor, valor == 100 ? "QUITADO" : "PARCIAL"); checks++;
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        var corridaEstorno = await Novo();
        var originalCorrida = await Criar(corridaEstorno, body with { Valor = 100 }, admin, adminId, "EMPRESA", 100, "QUITADO");
        var estornos = await Task.WhenAll(Send(HttpMethod.Post, $"/baixas/{originalCorrida.Baixa.Id}/estorno", admin, new EstornarBaixaRequest("Concorrência fictícia")),
            Send(HttpMethod.Post, $"/baixas/{originalCorrida.Baixa.Id}/estorno", financeiro, new EstornarBaixaRequest("Concorrência fictícia")));
        try
        {
            Check(estornos.Select(x => (int)x.StatusCode).Order().SequenceEqual(new[] { 200, 409 }), "estorno concorrente");
            Check(await db.Set<EstornoBaixa>().CountAsync(e => e.BaixaId == originalCorrida.Baixa.Id) == 1, "estorno duplicado persistido");
            await Saldo(corridaEstorno, admin, 0, "ABERTO"); checks++;
        }
        finally { foreach (var response in estornos) response.Dispose(); }
        foreach (var env in new[] { "Production", "Staging" })
        {
            var before = await Snapshot();
            var service = new BaixasService(db, new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = env });
            var p = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "ADMIN")], "Test"));
            Check(await service.CriarAsync(aberto, body, p, default) is IStatusCodeHttpResult { StatusCode: 403 }, "ambiente liberou baixa");
            Check(await service.EstornarAsync(p1.Baixa.Id, new("Fictício"), p, default) is IStatusCodeHttpResult { StatusCode: 403 }, "ambiente liberou estorno"); checks += 2;
            foreach (var kind in new[] { "saldo", "lista", "baixa" })
            {
                Check(await service.ConsultarAsync(kind, aberto, null, 1, 20, p, default) is IStatusCodeHttpResult { StatusCode: 403 }, "ambiente liberou consultas novas"); checks++;
            }
            Check(before == await Snapshot(), "bloqueio de ambiente gravou");
        }
        var development = new BaixasService(db, new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Development" });
        var adminIdentity = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "ADMIN"), new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim("empresa_id", a.ToString()), new Claim("tipo_usuario", "EMPRESA")], "Test"));
        foreach (var operacao in new[] { "criar", "estornar", "saldo", "lista", "baixa" })
        {
            var before = await Snapshot(); var rejected = false;
            try
            {
                if (operacao == "criar") await development.CriarAsync(aberto, body, adminIdentity, default);
                else if (operacao == "estornar") await development.EstornarAsync(p1.Baixa.Id, new("Fictício"), adminIdentity, default);
                else await development.ConsultarAsync(operacao, aberto, null, 1, 20, adminIdentity, default);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Conexão recusada")) { rejected = true; }
            Check(rejected && before == await Snapshot(), "Development aceitou conexão Testing"); checks++;
        }
        var consultaIdentity = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "CONSULTA")], "Test"));
        var beforeConsulta = await Snapshot();
        Check(await development.CriarAsync(aberto, body, consultaIdentity, default)
            is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult, "Development liberou baixa para CONSULTA");
        Check(await development.EstornarAsync(p1.Baixa.Id, new("Fictício"), consultaIdentity, default)
            is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult, "Development liberou estorno para CONSULTA");
        Check(beforeConsulta == await Snapshot(), "CONSULTA alterou dados"); checks += 2;
        foreach (var env in new[] { "Development", "Testing" })
            foreach (var state in new[] { EntityState.Added, EntityState.Modified, EntityState.Deleted })
                foreach (var isEstorno in new[] { false, true })
                {
                    var options = new DbContextOptionsBuilder<GestaoFinanceiraContext>().UseMySQL(db.Database.GetConnectionString()!)
                        .AddInterceptors(new FinancialWriteGuard(env)).Options;
                    await using var guarded = new GestaoFinanceiraContext(options);
                    object stub = isEstorno ? new EstornoBaixa { Id = long.MaxValue } : new BaixaFinanceira { Id = long.MaxValue };
                    guarded.Entry(stub).State = state;
                    var rejected = false;
                    try { await guarded.SaveChangesAsync(); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && guarded.Database.GetDbConnection().State == System.Data.ConnectionState.Closed, "guard liberou baixa/estorno fora da transação"); checks++;
                }
        Console.WriteLine($"[OK] BAIXAS_SCENARIO: {checks} verificações aprovadas; principal, status, estorno imutável, perfis, saldo, auditoria, rollback, concorrência e isolamento de ambientes.");
    }
}
