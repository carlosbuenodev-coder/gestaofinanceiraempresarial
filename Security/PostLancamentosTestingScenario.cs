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

namespace GestaoFinanceira.Security;

public static class PostLancamentosTestingScenario
{
    public static async Task RunAsync(IServiceProvider services, GestaoFinanceiraContext db, HttpClient http,
        long a, long b, long adminId, long financeiroId, long geralId,
        string admin, string financeiro, string consulta, string geral)
    {
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        async Task<long> Reference(string table, long empresa, string? tipo = null)
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = $"INSERT INTO `{table}` (empresa_id,nome" +
                (tipo is null ? ") VALUES (@empresa,@nome)" : ",tipo) VALUES (@empresa,@nome,@tipo)");
            void Add(string name, object value) { var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value; cmd.Parameters.Add(p); }
            Add("@empresa", empresa); Add("@nome", "Cadastro ficticio " + Guid.NewGuid().ToString("N"));
            if (tipo is not null) Add("@tipo", tipo);
            await cmd.ExecuteNonQueryAsync(); cmd.Parameters.Clear(); cmd.CommandText = "SELECT LAST_INSERT_ID()";
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        var receita = await Reference("categorias", a, "RECEITA");
        var despesa = await Reference("categorias", a, "DESPESA");
        var receitaB = await Reference("categorias", b, "RECEITA");
        var cliente = await Reference("clientes", a);
        var fornecedor = await Reference("fornecedores", a);
        var centro = await Reference("centros_custo", a);
        var clienteB = await Reference("clientes", b);
        var fornecedorB = await Reference("fornecedores", b);
        var centroB = await Reference("centros_custo", b);
        var valid = new CriarLancamentoRequest("RECEITA", "  Cadastro ficticio  ", receita,
            DataEmissao: new DateOnly(2026, 11, 1), DataVencimento: new DateOnly(2026, 11, 30), Valor: 123.45m);
        var checks = 0;
        async Task<HttpResponseMessage> Send(object body, string? token, string path = "/lancamentos")
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await http.SendAsync(req);
        }
        async Task Denied(object body, string? token, int status, string path = "/lancamentos")
        {
            var before = await Snapshot(db);
            using var result = await Send(body, token, path);
            if ((int)result.StatusCode != status)
            {
                Console.WriteLine($"[FALHOU] POST financeiro: esperado {status}; recebido {(int)result.StatusCode}.");
                throw new InvalidOperationException("POST financeiro inesperado.");
            }
            if (before != await Snapshot(db)) throw new InvalidOperationException("POST negado alterou dados financeiros.");
            checks++;
        }
        async Task Created(CriarLancamentoRequest body, string token, long empresa, long autorId, string autorTipo)
        {
            using var result = await Send(body, token);
            if ((int)result.StatusCode != 201)
            {
                Console.WriteLine($"[FALHOU] POST autorizado: recebido {(int)result.StatusCode}.");
                throw new InvalidOperationException("Cadastro autorizado falhou.");
            }
            var item = await result.Content.ReadFromJsonAsync<LancamentoResponse>() ?? throw new InvalidOperationException();
            if (item.Id <= 0 || item.EmpresaId != empresa || item.Status != "ABERTO" || item.Valor != body.Valor ||
                item.Descricao != body.Descricao!.Trim() || item.Tipo != body.Tipo || item.CategoriaId != body.CategoriaId ||
                item.ClienteId != body.ClienteId || item.FornecedorId != body.FornecedorId || item.CentroCustoId != body.CentroCustoId)
                throw new InvalidOperationException("DTO de cadastro incorreto.");
            var persisted = await db.Lancamentos.AsNoTracking().SingleAsync(x => x.Id == item.Id && x.EmpresaId == empresa);
            if (persisted.Status != "ABERTO" || persisted.Valor != body.Valor) throw new InvalidOperationException("Persistencia incorreta.");
            var audit = await db.Set<Auditoria>().AsNoTracking().SingleAsync(x => x.RegistroId == item.Id && x.Entidade == "lancamentos");
            using var detail = JsonDocument.Parse(audit.Detalhes);
            if (audit.EmpresaId != empresa || audit.Acao != "CRIAR" || audit.CriadoEm is null ||
                audit.UsuarioId != (autorTipo == "EMPRESA" ? autorId : (long?)null) ||
                detail.RootElement.GetProperty("autorId").GetInt64() != autorId ||
                detail.RootElement.GetProperty("autorTipo").GetString() != autorTipo)
                throw new InvalidOperationException("Autoria da auditoria incorreta.");
            using var getReq = new HttpRequestMessage(HttpMethod.Get, result.Headers.Location);
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var get = await http.SendAsync(getReq);
            var fetched = await get.Content.ReadFromJsonAsync<LancamentoResponse>();
            if ((int)get.StatusCode != 200 || fetched != item) throw new InvalidOperationException("GET do cadastro incorreto.");
            checks++;
        }
        await Created(valid, admin, a, adminId, "EMPRESA");
        await Created(valid with { Tipo = "DESPESA", CategoriaId = despesa, ClienteId = cliente,
            FornecedorId = fornecedor, CentroCustoId = centro, Observacoes = "Somente dados ficticios" }, financeiro, a, financeiroId, "EMPRESA");
        await Created(valid with { EmpresaId = a }, geral, a, geralId, "ADMIN_GERAL");
        await Created(valid with { EmpresaId = b, CategoriaId = receitaB }, geral, b, geralId, "ADMIN_GERAL");
        foreach (var body in new[] {
            valid with { Valor = 0 }, valid with { Valor = -1 }, valid with { Valor = null },
            valid with { Valor = 1.001m }, valid with { Valor = 10000000000000m },
            valid with { Descricao = " " }, valid with { Descricao = new string('x',256) },
            valid with { CategoriaId = null }, valid with { CategoriaId = long.MaxValue },
            valid with { CategoriaId = despesa }, valid with { CategoriaId = receitaB },
            valid with { ClienteId = clienteB }, valid with { FornecedorId = fornecedorB },
            valid with { CentroCustoId = centroB }, valid with { ClienteId = long.MaxValue },
            valid with { FornecedorId = long.MaxValue }, valid with { CentroCustoId = long.MaxValue },
            valid with { ClienteId = 0 }, valid with { CategoriaId = -1 },
            valid with { Tipo = "INVALIDO" }, valid with { DataEmissao = null },
            valid with { DataVencimento = null }, valid with { DataEmissao = DateOnly.MinValue },
            valid with { EmpresaId = a }, valid with { EmpresaId = b },
            valid with { Observacoes = new string('x',65536) } })
            await Denied(body, admin, 400);
        await Denied(valid with { EmpresaId = a }, financeiro, 400);
        await Denied(valid, admin, 400, $"/lancamentos?empresaId={a}");
        await Denied(valid, geral, 400);
        await Denied(valid with { EmpresaId = long.MaxValue }, geral, 403);
        await Denied(valid, consulta, 403);
        await Denied(valid, null, 401);
        await Denied(new { tipo = "RECEITA", descricao = "Ficticio", categoriaId = receita,
            dataEmissao = "2026-02-30", dataVencimento = "2026-03-01", valor = 1 }, admin, 400);
        await Denied(new { tipo = "RECEITA", descricao = "Ficticio", categoriaId = receita,
            dataEmissao = "2026-01-01", dataVencimento = "2026-03-01", valor = 1, status = "QUITADO" }, admin, 400);
        var failure = services.GetRequiredService<AuditFailureInterceptor>();
        var previousFailures = failure.Failures;
        failure.Arm();
        await Denied(valid, admin, 500);
        if (failure.Failures != previousFailures + 1) throw new InvalidOperationException("Falha de auditoria nao foi exercitada.");
        Console.WriteLine("[OK] Falha de auditoria apos INSERT do lancamento: rollback integral confirmado por fingerprint das duas tabelas.");
        await Created(valid, financeiro, a, financeiroId, "EMPRESA");
        var beforeDev = await Snapshot(db);
        var devService = new CadastroLancamentoService(db, new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Development" });
        var deniedDev = await devService.CriarAsync(valid, new ClaimsPrincipal(), CancellationToken.None);
        if (deniedDev is not Microsoft.AspNetCore.Http.IStatusCodeHttpResult { StatusCode: 403 } || beforeDev != await Snapshot(db))
            throw new InvalidOperationException("Bloqueio de Development incorreto.");
        checks++;
        var blockedOptions = new DbContextOptionsBuilder<GestaoFinanceiraContext>()
            .UseMySQL(db.Database.GetConnectionString()!).AddInterceptors(new FinancialWriteGuard(false)).Options;
        await using (var blockedDb = new GestaoFinanceiraContext(blockedOptions))
        {
            blockedDb.Lancamentos.Add(new Lancamento { EmpresaId = a });
            var refused = false;
            try { await blockedDb.SaveChangesAsync(); }
            catch (InvalidOperationException) { refused = true; }
            if (!refused || beforeDev != await Snapshot(db)) throw new InvalidOperationException("Interceptor permitiu escrita fora de Testing.");
        }
        checks++;
        Console.WriteLine($"[OK] POST_FINANCE_SCENARIO: {checks} casos aprovados, com auditoria, rollback e bloqueio de Development.");
    }

    private static async Task<string> Snapshot(GestaoFinanceiraContext db)
    {
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var table in new[] { "lancamentos", "auditoria" })
        {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = $"SELECT * FROM `{table}` ORDER BY id";
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
