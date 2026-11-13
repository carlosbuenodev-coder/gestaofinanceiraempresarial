using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Security;

public static class TestingScenario
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GestaoFinanceiraContext>();
        await db.Database.OpenConnectionAsync();
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        var execution = Guid.NewGuid().ToString("N");
        var a = new Empresa { RazaoSocial = "Empresa Ficticia Seguranca A", Ativo = true };
        var b = new Empresa { RazaoSocial = "Empresa Ficticia Seguranca B", Ativo = true };
        db.Empresas.AddRange(a, b);
        await db.SaveChangesAsync();
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36)) + "Aa1!";
        Usuario Add(Empresa company, string label, string role)
        {
            var user = new Usuario { EmpresaId = company.Id, Nome = "Ficticio " + label,
                Email = label + "-" + execution + "@seguranca.invalid", Perfil = role, Ativo = true };
            user.SenhaHash = new PasswordHasher<Usuario>().HashPassword(user, password);
            db.Usuarios.Add(user);
            return user;
        }
        var adminA = Add(a, "admin-a", "ADMIN");
        var financeA = Add(a, "financeiro-a", "FINANCEIRO");
        var consultaA = Add(a, "consulta-a", "CONSULTA");
        var adminB = Add(b, "admin-b", "ADMIN");
        var financeB = Add(b, "financeiro-b", "FINANCEIRO");
        Add(b, "consulta-b", "CONSULTA");
        var geral = new AdministradorGeral { Nome = "Administrador Geral Ficticio",
            Email = "geral-" + execution + "@seguranca.invalid", Ativo = true };
        geral.SenhaHash = new PasswordHasher<AdministradorGeral>().HashPassword(geral, password);
        db.AdministradoresGerais.Add(geral);
        await db.SaveChangesAsync();
        Console.WriteLine("[OK] Fixtures: duas empresas, seis usuários empresariais e um administrador geral; hashes gerados localmente.");

        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { BaseAddress = new Uri("http://localhost:5217"), Timeout = TimeSpan.FromSeconds(20) };
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = JsonContent.Create(body);
            return await http.SendAsync(request);
        }
        async Task Expect(HttpMethod method, string path, string? token, object? body, int expected)
        {
            using var response = await Send(method, path, token, body);
            if ((int)response.StatusCode != expected)
            {
                Console.WriteLine($"[FALHOU] {method} {path}: esperado {expected}; recebido {(int)response.StatusCode}");
                throw new InvalidOperationException("Resposta inesperada; testes restantes não executados.");
            }
            Console.WriteLine($"[OK] {method} {path}: HTTP {expected}");
        }
        async Task<string> Login(Usuario user)
        {
            using var response = await Send(HttpMethod.Post, "/auth/usuarios/login", null,
                new { empresaId = user.EmpresaId, email = user.Email, senha = password });
            if ((int)response.StatusCode != 200) throw new InvalidOperationException("Login fictício falhou.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Console.WriteLine($"[OK] Login {user.Perfil}, empresa {user.EmpresaId}: HTTP 200 (JWT omitido)");
            return json.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException();
        }
        var tokenA = await Login(adminA);
        var tokenFinance = await Login(financeA);
        var tokenConsulta = await Login(consultaA);
        var tokenB = await Login(adminB);
        using var geralLogin = await Send(HttpMethod.Post, "/auth/admin/login", null,
            new { email = geral.Email, senha = password });
        if ((int)geralLogin.StatusCode != 200) throw new InvalidOperationException("Login geral falhou.");
        using var geralJson = JsonDocument.Parse(await geralLogin.Content.ReadAsStringAsync());
        var tokenGeral = geralJson.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException();
        Console.WriteLine("[OK] Login ADMIN_GERAL: HTTP 200 (JWT omitido)");

        await FinanceTestingScenario.RunAsync(db, http, a.Id, b.Id, tokenA, tokenFinance, tokenConsulta, tokenB, tokenGeral);
        await PostLancamentosTestingScenario.RunAsync(services, db, http, a.Id, b.Id,
            adminA.Id, financeA.Id, geral.Id, tokenA, tokenFinance, tokenConsulta, tokenGeral);
        var postBody = new CriarLancamentoRequest("RECEITA", "Cadastro ficticio bloqueado", 1,
            DataEmissao: new DateOnly(2026, 11, 1), DataVencimento: new DateOnly(2026, 11, 30), Valor: 1);


        await Expect(HttpMethod.Get, "/empresas", tokenGeral, null, 200);
        await Expect(HttpMethod.Get, $"/empresas/{a.Id}", tokenGeral, null, 200);
        await Expect(HttpMethod.Get, "/empresas", tokenA, null, 403);
        await Expect(HttpMethod.Get, "/empresas", null, null, 401);
        await Expect(HttpMethod.Get, $"/usuarios/{financeA.Id}", tokenA, null, 200);
        object Body(Usuario u) => new { nome = "Ficticio Atualizado", email = u.Email, perfil = "CONSULTA" };
        const long missing = long.MaxValue;
        if (await db.Usuarios.AnyAsync(u => u.Id == missing)) throw new InvalidOperationException("ID não está ausente.");
        Console.WriteLine("[OK] ID inexistente confirmado por SELECT no banco exclusivo de testes.");
        async Task Denied(HttpMethod method, long id, string? token, int status, object? body = null)
        {
            var before = await Fingerprint(db);
            await Expect(method, method == HttpMethod.Patch ? $"/usuarios/{id}/desativar" : $"/usuarios/{id}", token, body, status);
            if (before != await Fingerprint(db))
                throw new InvalidOperationException("Operação negada alterou registros.");
            Console.WriteLine("[OK] Todos os campos das três tabelas preservados, incluindo hashes (não exibidos).");
        }

        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch })
        {
            await Denied(method, financeB.Id, tokenA, 404, method == HttpMethod.Put ? Body(financeB) : null);
            await Denied(method, adminB.Id, tokenA, 404, method == HttpMethod.Put ? Body(adminB) : null);
            await Denied(method, missing, tokenA, 404, method == HttpMethod.Put ? Body(financeA) : null);
            await Denied(method, adminA.Id, tokenA, 403, method == HttpMethod.Put ? Body(adminA) : null);
            await Denied(method, financeA.Id, null, 401, method == HttpMethod.Put ? Body(financeA) : null);
            foreach (var token in new[] { tokenFinance, tokenConsulta })
            {
                await Denied(method, financeA.Id, token, 403, method == HttpMethod.Put ? Body(financeA) : null);
                await Denied(method, missing, token, 403, method == HttpMethod.Put ? Body(financeA) : null);
            }
        }
        foreach (var token in new[] { tokenA, tokenFinance, tokenConsulta, tokenB })
            await Expect(HttpMethod.Get, "/usuarios/me", token, null, 200);
        await Expect(HttpMethod.Get, $"/usuarios/{financeB.Id}", tokenA, null, 404);
        await Expect(HttpMethod.Get, "/usuarios/minha-empresa", tokenA, null, 200);

        // Allowed writes target only disposable fixtures; verify actual persistence.
        await Expect(HttpMethod.Put, $"/usuarios/{financeA.Id}", tokenA, Body(financeA), 200);
        var updated = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == financeA.Id);
        if (updated.Nome != "Ficticio Atualizado" || updated.Perfil != "CONSULTA" ||
            updated.SenhaHash != financeA.SenhaHash || updated.EmpresaId != a.Id)
            throw new InvalidOperationException("Alteração permitida não corresponde ao esperado.");
        await Expect(HttpMethod.Patch, $"/usuarios/{consultaA.Id}/desativar", tokenA, null, 200);
        if (await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == consultaA.Id && u.Ativo))
            throw new InvalidOperationException("Desativação não persistiu.");
        var oldProfileFingerprint = await Fingerprint(db);
        await Expect(HttpMethod.Post, "/lancamentos", tokenFinance, postBody, 403);
        await Expect(HttpMethod.Post, "/lancamentos", tokenConsulta, postBody, 403);
        await Expect(HttpMethod.Get, "/lancamentos", tokenFinance, null, 403);
        await Expect(HttpMethod.Get, "/lancamentos", tokenConsulta, null, 403);
        await Expect(HttpMethod.Get, $"/lancamentos/{long.MaxValue}", tokenFinance, null, 403);
        await Expect(HttpMethod.Get, $"/lancamentos/{long.MaxValue}", tokenConsulta, null, 403);
        await Expect(HttpMethod.Get, "/usuarios/me", tokenFinance, null, 403);
        await Expect(HttpMethod.Get, "/usuarios/me", tokenConsulta, null, 403);
        if (oldProfileFingerprint != await Fingerprint(db)) throw new InvalidOperationException();
        await Denied(HttpMethod.Put, financeA.Id, tokenA, 400,
            new { nome = updated.Nome, email = updated.Email, perfil = "ADMIN" });
        await Expect(HttpMethod.Put, $"/usuarios/{adminB.Id}", tokenGeral,
            new { nome = "Admin B Ficticio Atualizado", email = adminB.Email, perfil = "ADMIN" }, 200);
        await Expect(HttpMethod.Patch, $"/usuarios/{adminB.Id}/desativar", tokenGeral, null, 200);
        var finalB = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == adminB.Id);
        if (finalB.Ativo || finalB.Nome != "Admin B Ficticio Atualizado" || finalB.EmpresaId != b.Id)
            throw new InvalidOperationException("Operações ADMIN_GERAL não persistiram.");
        await Expect(HttpMethod.Get, "/usuarios/me", tokenB, null, 403);
        await Expect(HttpMethod.Post, "/lancamentos", tokenB, postBody, 403);
        Console.WriteLine("[OK] Operações permitidas persistidas somente em fixtures; bloqueios após desativação/rebaixamento confirmados.");
        geral.Ativo = false;
        await db.SaveChangesAsync();
        await Expect(HttpMethod.Get, $"/lancamentos?empresaId={a.Id}", tokenGeral, null, 403);
        await Expect(HttpMethod.Get, $"/lancamentos/{long.MaxValue}?empresaId={a.Id}", tokenGeral, null, 403);
        await Expect(HttpMethod.Post, "/lancamentos", tokenGeral, postBody with { EmpresaId = a.Id }, 403);
        a.Ativo = false;
        await db.SaveChangesAsync();
        await Expect(HttpMethod.Post, "/lancamentos", tokenA, postBody, 403);
        await Expect(HttpMethod.Post, "/lancamentos", tokenGeral, postBody with { EmpresaId = a.Id }, 403);
        await Expect(HttpMethod.Get, "/lancamentos", tokenA, null, 403);
        await Expect(HttpMethod.Get, $"/lancamentos/{long.MaxValue}", tokenA, null, 403);
        Console.WriteLine("[OK] SECURITY_SCENARIO_COMPLETE — nenhum registro excluído.");
    }

    private static async Task<string> Fingerprint(GestaoFinanceiraContext db)
    {
        var connection = db.Database.GetDbConnection();
        await DatabaseSafety.VerifyOpenAsync(connection, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var table in new[] { "empresas", "usuarios", "administradores_gerais" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM `{table}` ORDER BY id";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fields = new string?[reader.FieldCount];
                for (var i = 0; i < fields.Length; i++)
                    fields[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
                hash.AppendData(Encoding.UTF8.GetBytes(table + JsonSerializer.Serialize(fields)));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
