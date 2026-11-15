using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GestaoFinanceira.Security;

public static class AlteracaoLancamentosTestingScenario
{
    public static async Task RunAsync(IServiceProvider services, GestaoFinanceiraContext db, HttpClient http,
        long a, long b, long adminId, long financeiroId, long geralId,
        string admin, string financeiro, string consulta, string adminB, string geral)
    {
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        var checks = 0;
        static void Check(bool valid, string label)
        {
            if (!valid)
            {
                Console.WriteLine("[FALHOU] Alteração financeira: " + label);
                throw new InvalidOperationException("Alteração financeira: " + label);
            }
        }
        async Task<long> Insert(string sql, params object[] values)
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            for (var i = 0; i < values.Length; i++)
            {
                var p = cmd.CreateParameter(); p.ParameterName = "@p" + i; p.Value = values[i]; cmd.Parameters.Add(p);
            }
            await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear(); cmd.CommandText = "SELECT LAST_INSERT_ID()";
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        Task<long> Ref(string table, long empresa) => Insert($"INSERT INTO `{table}` (empresa_id,nome) VALUES (@p0,@p1)", empresa, "Fixture edição");
        var cat = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,@p1,'RECEITA')", a, "Fixture edição");
        var cat2 = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,@p1,'RECEITA')", a, "Fixture edição alternativa");
        var despesa = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,@p1,'DESPESA')", a, "Fixture despesa");
        var catB = await Insert("INSERT INTO categorias (empresa_id,nome,tipo) VALUES (@p0,@p1,'RECEITA')", b, "Fixture outra empresa");
        var cliente = await Ref("clientes", a); var clienteB = await Ref("clientes", b);
        var fornecedor = await Ref("fornecedores", a); var fornecedorB = await Ref("fornecedores", b);
        var centro = await Ref("centros_custo", a); var centroB = await Ref("centros_custo", b);
        async Task<long> Novo(string status = "ABERTO", long? empresa = null)
        {
            var x = new Lancamento { EmpresaId = empresa ?? a, CategoriaId = empresa == b ? catB : cat,
                Tipo = "RECEITA", Descricao = "Original fictício edição", Valor = 100,
                DataEmissao = new DateTime(2026, 11, 1), DataVencimento = new DateTime(2026, 11, 20), Status = status };
            db.Lancamentos.Add(x); await db.SaveChangesAsync(); return x.Id;
        }
        var body = new AtualizarLancamentoRequest("Editado fictício", cat2, cliente, fornecedor, centro,
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 25), 234.56m, "Observações fictícias");
        async Task<LancamentoResponse> State(long id)
        {
            var x = await db.Lancamentos.AsNoTracking().SingleAsync(x => x.Id == id);
            return new(x.Id, x.EmpresaId, x.Tipo, x.Descricao, x.ClienteId, x.FornecedorId, x.CategoriaId,
                x.CentroCustoId, x.DataEmissao, x.DataVencimento, x.Valor, x.Status, x.Observacoes, x.CriadoEm);
        }
        async Task<HttpResponseMessage> Send(HttpMethod method, long id, object value, string? token, string suffix = "")
        {
            using var req = new HttpRequestMessage(method, $"/lancamentos/{id}" + (method == HttpMethod.Post ? "/cancelamento" : "") + suffix)
                { Content = JsonContent.Create(value) };
            if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await http.SendAsync(req);
        }
        async Task Denied(HttpMethod method, long id, object value, string? token, int expected, string suffix = "")
        {
            var before = await Snapshot(db);
            using var response = await Send(method, id, value, token, suffix);
            Check((int)response.StatusCode == expected, $"HTTP esperado {expected}, recebido {(int)response.StatusCode}");
            Check(before == await Snapshot(db), "requisição bloqueada alterou dados");
            checks++;
        }
        async Task Success(HttpMethod method, long id, object value, string token, long empresa, long autor, string tipoAutor)
        {
            var before = await State(id);
            using var result = await Send(method, id, value, token);
            Check((int)result.StatusCode == 200, "operação autorizada falhou");
            var item = await result.Content.ReadFromJsonAsync<LancamentoResponse>();
            Check(item is not null && item == await State(id), "DTO diverge da persistência");
            Check(item!.Id == before.Id && item.EmpresaId == before.EmpresaId && item.Tipo == before.Tipo && item.CriadoEm == before.CriadoEm, "campos imutáveis alterados");
            if (method == HttpMethod.Put)
            {
                var update = (AtualizarLancamentoRequest)value;
                Check(item.Status == before.Status && item.Descricao == update.Descricao && item.Valor == update.Valor &&
                    item.CategoriaId == update.CategoriaId && item.ClienteId == update.ClienteId && item.FornecedorId == update.FornecedorId &&
                    item.CentroCustoId == update.CentroCustoId && item.Observacoes == update.Observacoes &&
                    DateOnly.FromDateTime(item.DataEmissao) == update.DataEmissao && DateOnly.FromDateTime(item.DataVencimento) == update.DataVencimento, "edição incompleta");
            }
            else Check(item == before with { Status = "CANCELADO" }, "cancelamento alterou outros campos");
            var acao = method == HttpMethod.Put ? "ALTERAR" : "CANCELAR";
            var audit = await db.Set<Auditoria>().AsNoTracking().SingleAsync(x => x.RegistroId == id && x.Entidade == "lancamentos" && x.Acao == acao);
            using var detail = JsonDocument.Parse(audit.Detalhes);
            var root = detail.RootElement;
            Check(audit.EmpresaId == empresa && audit.CriadoEm is not null && audit.UsuarioId == (tipoAutor == "EMPRESA" ? autor : (long?)null) &&
                root.GetProperty("autorId").GetInt64() == autor && root.GetProperty("autorTipo").GetString() == tipoAutor &&
                root.GetProperty("anterior").Deserialize<LancamentoResponse>() == before &&
                root.GetProperty("posterior").Deserialize<LancamentoResponse>() == item, "auditoria incorreta");
            using var getReq = new HttpRequestMessage(HttpMethod.Get, $"/lancamentos/{id}" + (tipoAutor == "ADMIN_GERAL" ? $"?empresaId={empresa}" : ""));
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var get = await http.SendAsync(getReq);
            Check((int)get.StatusCode == 200 && await get.Content.ReadFromJsonAsync<LancamentoResponse>() == item, "regressão GET");
            checks++;
        }
        foreach (var (token, autor, tipo) in new[] { (admin, adminId, "EMPRESA"), (financeiro, financeiroId, "EMPRESA"), (geral, geralId, "ADMIN_GERAL") })
        {
            var id = await Novo();
            await Success(HttpMethod.Put, id, body with { EmpresaId = tipo == "ADMIN_GERAL" ? a : null }, token, a, autor, tipo);
            await Success(HttpMethod.Post, id, new CancelarLancamentoRequest(tipo == "ADMIN_GERAL" ? a : null), token, a, autor, tipo);
            await Denied(HttpMethod.Post, id, new CancelarLancamentoRequest(tipo == "ADMIN_GERAL" ? a : null), token, 409);
        }
        var other = await Novo(empresa: b);
        await Success(HttpMethod.Put, other, body with { CategoriaId = catB, ClienteId = null, FornecedorId = null, CentroCustoId = null, EmpresaId = b }, geral, b, geralId, "ADMIN_GERAL");
        await Success(HttpMethod.Post, other, new CancelarLancamentoRequest(b), geral, b, geralId, "ADMIN_GERAL");
        var current = await Novo();
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Post })
        {
            object valid = method == HttpMethod.Put ? body : new CancelarLancamentoRequest();
            await Denied(method, current, valid, consulta, 403);
            await Denied(method, current, valid, null, 401);
            await Denied(method, current, valid, "invalid.jwt.token", 401);
            var expired = new JwtSecurityToken(issuer: "GestaoFinanceiraAPI.Testing", audience: "GestaoFinanceiraAPI.Testing",
                claims: [new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Role, "ADMIN"),
                    new Claim("empresa_id", a.ToString()), new Claim("tipo_usuario", "EMPRESA")],
                notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(Environment.GetEnvironmentVariable("Testing__JwtKey")!)), SecurityAlgorithms.HmacSha256));
            await Denied(method, current, valid, new JwtSecurityTokenHandler().WriteToken(expired), 401);
            await Denied(method, current, valid, adminB, 404);
            await Denied(method, long.MaxValue, valid, admin, 404);
            await Denied(method, -1, valid, admin, 404);
            await Denied(method, other, valid, admin, 404);
            await Denied(method, current, valid, geral, 400);
            await Denied(method, current, method == HttpMethod.Put ? body with { EmpresaId = b } : new CancelarLancamentoRequest(b), admin, 400);
            await Denied(method, current, method == HttpMethod.Put ? body with { EmpresaId = b } : new CancelarLancamentoRequest(b), geral, 404);
            await Denied(method, current, method == HttpMethod.Put ? body with { EmpresaId = 0 } : new CancelarLancamentoRequest(0), geral, 400);
            await Denied(method, current, valid, admin, 400, $"?empresaId={a}");
            foreach (var status in new[] { "PARCIAL", "QUITADO", "CANCELADO" })
                await Denied(method, await Novo(status), valid, admin, 409);
        }
        foreach (var invalid in new[] { body with { CategoriaId = catB }, body with { CategoriaId = despesa },
            body with { ClienteId = clienteB }, body with { FornecedorId = fornecedorB }, body with { CentroCustoId = centroB },
            body with { CategoriaId = long.MaxValue }, body with { ClienteId = long.MaxValue }, body with { FornecedorId = long.MaxValue }, body with { CentroCustoId = long.MaxValue },
            body with { CategoriaId = null }, body with { ClienteId = 0 }, body with { Valor = 0 }, body with { Valor = -1 },
            body with { Valor = 1.001m }, body with { Valor = 10000000000000m }, body with { Valor = null },
            body with { Descricao = " " }, body with { Descricao = new string('x', 256) }, body with { DataEmissao = null },
            body with { DataVencimento = null }, body with { DataEmissao = DateOnly.MinValue }, body with { DataVencimento = DateOnly.MinValue },
            body with { Observacoes = new string('ç', 32768) } })
            await Denied(HttpMethod.Put, current, invalid, admin, 400);
        await Denied(HttpMethod.Put, current, new { descricao = "Fictício", categoriaId = cat, dataEmissao = "2026-02-30", dataVencimento = "2026-11-30", valor = 10 }, admin, 400);
        foreach (var field in new[] { "id", "tipo", "status", "criadoEm" })
        {
            var value = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(body))!;
            value[field] = field == "id" ? 1 : "CANCELADO";
            await Denied(HttpMethod.Put, current, value, admin, 400);
            await Denied(HttpMethod.Post, current, new Dictionary<string, object?> { [field] = "CANCELADO" }, admin, 400);
        }
        var paid = await Novo();
        var account = await Insert("INSERT INTO contas_bancarias (empresa_id,descricao) VALUES (@p0,@p1)", a, "Conta fictícia edição");
        var paymentMethod = await Insert("INSERT INTO formas_pagamento (nome) VALUES (@p0)", "Fixture-" + Guid.NewGuid().ToString("N"));
        await Insert("INSERT INTO baixas (lancamento_id,conta_bancaria_id,forma_pagamento_id,data_pagamento,valor) VALUES (@p0,@p1,@p2,'2026-11-01',10)", paid, account, paymentMethod);
        await Denied(HttpMethod.Put, paid, body, admin, 409);
        await Denied(HttpMethod.Post, paid, new CancelarLancamentoRequest(), admin, 409);
        // Failure occurs after the UPDATE was sent, proving transactional rollback for both operations.
        var failure = services.GetRequiredService<AuditFailureInterceptor>();
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Post })
        {
            var failures = failure.Failures;
            failure.Arm();
            await Denied(method, current, method == HttpMethod.Put ? body : new CancelarLancamentoRequest(), admin, 500);
            Check(failure.Failures == failures + 1, "falha de auditoria não exercitada");
        }
        var race = await Novo();
        var races = await Task.WhenAll(Send(HttpMethod.Post, race, new CancelarLancamentoRequest(), admin), Send(HttpMethod.Post, race, new CancelarLancamentoRequest(), admin));
        try
        {
            Check(races.Select(r => (int)r.StatusCode).Order().SequenceEqual(new[] { 200, 409 }), "cancelamento concorrente");
            Check(await db.Set<Auditoria>().CountAsync(x => x.RegistroId == race && x.Acao == "CANCELAR") == 1, "auditoria duplicada");
            checks++;
        }
        finally { foreach (var r in races) r.Dispose(); }
        // Check application environment guards against a real Testing context, never a Development DB.
        var developmentIdentity = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Role, "ADMIN"),
            new Claim("empresa_id", a.ToString()), new Claim("tipo_usuario", "EMPRESA") }, "Test"));
        foreach (var env in new[] { "Production", "Staging" })
        {
            var before = await Snapshot(db);
            var service = new AlteracaoLancamentoService(db, new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = env });
            Check(await service.AtualizarAsync(current, body, developmentIdentity, default) is IStatusCodeHttpResult { StatusCode: 403 }, "ambiente liberou PUT");
            Check(await service.CancelarAsync(current, new(), developmentIdentity, default) is IStatusCodeHttpResult { StatusCode: 403 }, "ambiente liberou cancelamento");
            Check(before == await Snapshot(db), "ambiente bloqueado gravou dados"); checks += 2;
        }
        // Enabled Development must reject a Testing connection, without writing to either database.
        var developmentService = new AlteracaoLancamentoService(db,
            new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Development" });
        var consultaIdentity = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "CONSULTA")], "Test"));
        var beforeConsulta = await Snapshot(db);
        Check(await developmentService.AtualizarAsync(current, body, consultaIdentity, default)
            is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult, "Development liberou edição para CONSULTA");
        Check(await developmentService.CancelarAsync(current, new(), consultaIdentity, default)
            is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult, "Development liberou cancelamento para CONSULTA");
        Check(beforeConsulta == await Snapshot(db), "CONSULTA alterou dados"); checks += 2;
        foreach (var cancel in new[] { false, true })
        {
            var before = await Snapshot(db);
            var rejected = false;
            try
            {
                if (cancel) await developmentService.CancelarAsync(current, new(), developmentIdentity, default);
                else await developmentService.AtualizarAsync(current, body, developmentIdentity, default);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Conexão recusada")) { rejected = true; }
            Check(rejected && before == await Snapshot(db), "Development aceitou conexão Testing"); checks++;
        }
        // The interceptor must independently refuse the wrong destination before any SQL writes.
        foreach (var cancel in new[] { false, true })
        {
            var options = new DbContextOptionsBuilder<GestaoFinanceiraContext>().UseMySQL(db.Database.GetConnectionString()!)
                .AddInterceptors(new FinancialWriteGuard("Development")).Options;
            await using var guarded = new GestaoFinanceiraContext(options);
            var stub = new Lancamento { Id = current, Status = "ABERTO", Descricao = "Antes" };
            guarded.Attach(stub); if (cancel) stub.Status = "CANCELADO"; else stub.Descricao = "Depois";
            var rejected = false;
            try { await guarded.SaveChangesAsync(); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Conexão recusada")) { rejected = true; }
            Check(rejected && guarded.Database.GetDbConnection().State == System.Data.ConnectionState.Closed, "interceptor Development aceitou conexão Testing"); checks++;
        }
        Console.WriteLine($"[OK] ALTERACAO_FINANCE_SCENARIO: {checks} casos aprovados; edição, cancelamento, JWT, referências, baixas, auditoria, rollback, concorrência e isolamento dos ambientes.");
    }

    internal static async Task<string> Snapshot(GestaoFinanceiraContext db)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var table in new[] { "lancamentos", "auditoria", "baixas", "contas_bancarias", "formas_pagamento" })
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = $"SELECT * FROM `{table}` ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new object[reader.FieldCount]; reader.GetValues(row);
                hash.AppendData(Encoding.UTF8.GetBytes(table + JsonSerializer.Serialize(row)));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
