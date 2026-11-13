using System.Net.Http.Headers;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Security;

public static class FinanceTestingScenario
{
    public static async Task RunAsync(GestaoFinanceiraContext db, HttpClient http,
        long a, long b, string admin, string financeiro, string consulta, string adminB, string geral)
    {
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        async Task<long> Reference(string table, long empresa)
        {
            // Table comes exclusively from the hardcoded calls below; values are parameters.
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = $"INSERT INTO `{table}` (empresa_id,nome" +
                (table == "categorias" ? ",tipo) VALUES (@empresa,'Fixture financeira','RECEITA')" : ") VALUES (@empresa,'Fixture financeira')");
            var param = cmd.CreateParameter(); param.ParameterName = "@empresa"; param.Value = empresa;
            cmd.Parameters.Add(param);
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "SELECT LAST_INSERT_ID()"; cmd.Parameters.Clear();
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        var categoria = await Reference("categorias", a);
        var categoriaB = await Reference("categorias", b);
        var cliente = await Reference("clientes", a);
        var fornecedor = await Reference("fornecedores", a);
        var centro = await Reference("centros_custo", a);
        Lancamento Novo(long empresa, long cat, int dia, decimal valor, string tipo, string status) => new()
        {
            EmpresaId = empresa, CategoriaId = cat, Descricao = "Lancamento ficticio",
            Tipo = tipo, Status = status, Valor = valor,
            DataEmissao = new DateTime(2026, 1, 1), DataVencimento = new DateTime(2026, 2, dia),
            ClienteId = empresa == a ? cliente : null, FornecedorId = empresa == a ? fornecedor : null,
            CentroCustoId = empresa == a ? centro : null
        };
        var l1 = Novo(a, categoria, 10, 100.25m, "RECEITA", "ABERTO");
        var l2 = Novo(a, categoria, 10, 200m, "DESPESA", "PARCIAL");
        var l3 = Novo(a, categoria, 20, 300m, "RECEITA", "QUITADO");
        var outro = Novo(b, categoriaB, 10, 999m, "RECEITA", "ABERTO");
        db.Lancamentos.AddRange(l1, l2, l3, outro);
        await db.SaveChangesAsync();
        var checks = 0;
        async Task<JsonDocument?> Get(string path, string? token, int expected)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request);
            if ((int)response.StatusCode != expected)
            {
                Console.WriteLine($"[FALHOU] Financeiro {path}: esperado {expected}, recebido {(int)response.StatusCode}.");
                throw new InvalidOperationException("Resposta financeira inesperada.");
            }
            checks++;
            return expected == 200 ? JsonDocument.Parse(await response.Content.ReadAsStringAsync()) : null;
        }
        async Task Page(string query, string token, long empresa, long total, params long[] ids)
        {
            using var json = await Get("/lancamentos" + query, token, 200);
            var root = json!.RootElement;
            var items = root.GetProperty("itens").EnumerateArray().ToArray();
            if (root.GetProperty("total").GetInt64() != total ||
                items.Any(x => x.GetProperty("empresaId").GetInt64() != empresa) ||
                !items.Select(x => x.GetProperty("id").GetInt64()).SequenceEqual(ids))
                throw new InvalidOperationException("Conteúdo, contagem ou ordem financeira incorretos.");
        }
        foreach (var token in new[] { admin, financeiro, consulta })
        {
            await Page("?ordenarPor=id", token, a, 3, l1.Id, l2.Id, l3.Id);
            using var detail = await Get($"/lancamentos/{l1.Id}", token, 200);
            if (detail!.RootElement.GetProperty("valor").GetDecimal() != 100.25m) throw new InvalidOperationException("Precisão decimal incorreta.");
            await Get($"/lancamentos/{outro.Id}", token, 404);
            await Get($"/lancamentos/{long.MaxValue}", token, 404);
            await Get($"/lancamentos?empresaId={b}", token, 403);
            await Get($"/lancamentos/{outro.Id}?empresaId={b}", token, 403);
        }
        await Page("?ordenarPor=id", adminB, b, 1, outro.Id);
        await Get($"/lancamentos/{l1.Id}", adminB, 404);
        await Page($"?empresaId={a}&ordenarPor=id", geral, a, 3, l1.Id, l2.Id, l3.Id);
        await Page($"?empresaId={b}", geral, b, 1, outro.Id);
        await Get("/lancamentos", geral, 400);
        await Get($"/lancamentos/{l1.Id}", geral, 400);
        await Get($"/lancamentos/{outro.Id}?empresaId={a}", geral, 404);
        using var geralDetail = await Get($"/lancamentos/{outro.Id}?empresaId={b}", geral, 200);
        await Page("?tipo=DESPESA", admin, a, 1, l2.Id);
        await Page("?status=QUITADO", admin, a, 1, l3.Id);
        foreach (var field in new[] { $"categoriaId={categoria}", $"clienteId={cliente}",
            $"fornecedorId={fornecedor}", $"centroCustoId={centro}" })
            await Page("?" + field + "&ordenarPor=id", admin, a, 3, l1.Id, l2.Id, l3.Id);
        await Page($"?categoriaId={categoriaB}", admin, a, 0);
        await Page("?vencimentoInicio=2026-02-10&vencimentoFim=2026-02-10", admin, a, 2, l1.Id, l2.Id);
        await Page("?tipo=RECEITA&status=ABERTO&categoriaId=" + categoria, admin, a, 1, l1.Id);
        await Page("?tamanhoPagina=1&pagina=2", admin, a, 3, l2.Id);
        await Page("?ordenarPor=valor&direcao=desc&tamanhoPagina=2", admin, a, 3, l3.Id, l2.Id);
        await Page("?ordenarPor=dataEmissao", admin, a, 3, l1.Id, l2.Id, l3.Id);
        await Page("?pagina=10", admin, a, 3);
        await Page("?status=CANCELADO", admin, a, 0);
        await Get("/lancamentos/-1", admin, 404);
        using var metadata = await Get("/lancamentos?tamanhoPagina=2&pagina=2", admin, 200);
        var page = metadata!.RootElement;
        if (page.GetProperty("pagina").GetInt32() != 2 || page.GetProperty("tamanhoPagina").GetInt32() != 2 ||
            page.GetProperty("totalPaginas").GetInt64() != 2 || page.GetProperty("total").GetInt64() != 3 ||
            page.GetProperty("itens")[0].GetProperty("id").GetInt64() != l3.Id)
            throw new InvalidOperationException("Metadados de pagina incorretos.");
        foreach (var query in new[] { "tipo=INVALIDO", "status=INVALIDO", "categoriaId=0", "clienteId=-1",
            "fornecedorId=0", "centroCustoId=-1", "empresaId=0", "pagina=0", "tamanhoPagina=101",
            "pagina=2147483647&tamanhoPagina=100", "ordenarPor=valor%3BDROP", "direcao=INVALIDA",
            "vencimentoInicio=2026-03-01&vencimentoFim=2026-02-01", "vencimentoInicio=invalido", "pagina=abc" })
            await Get("/lancamentos?" + query, admin, 400);
        await Get("/lancamentos", null, 401);
        await Get($"/lancamentos/{l1.Id}", null, 401);
        Console.WriteLine($"[OK] FINANCE_SCENARIO: {checks} verificações HTTP com conteúdo, filtros, precisão, paginação e isolamento.");
    }
}
