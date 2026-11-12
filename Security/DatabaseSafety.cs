using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;

namespace GestaoFinanceira.Security;

public static class DatabaseSafety
{
    public const string Original = "gestao_financeira_empresarial";
    public const string TestDatabase = "gestao_financeira_seguranca_testes";

    public static async Task VerifyAsync(string connectionString, bool testing)
    {
        try
        {
            var settings = new MySqlConnectionStringBuilder(connectionString);
            var expectedDatabase = testing ? TestDatabase : Original;
            var expectedUser = testing ? "gestao_api_testing" : "gestao_api_dev";
            if (settings.Database != expectedDatabase || settings.UserID != expectedUser ||
                settings.Server != "localhost" || settings.Port != 3306)
                throw new InvalidOperationException();
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            await VerifyOpenAsync(connection, testing);
        }
        catch
        {
            throw new InvalidOperationException("Conexão recusada: banco, identidade ou privilégios não correspondem ao ambiente.");
        }
    }

    public static async Task VerifyOpenAsync(DbConnection connection, bool testing)
    {
        var database = testing ? TestDatabase : Original;
        var user = testing ? "gestao_api_testing" : "gestao_api_dev";
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT DATABASE(), CURRENT_USER(), CURRENT_ROLE()";
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != database ||
                reader.GetString(1) != user + "@localhost" || reader.GetString(2) != "NONE")
                throw new InvalidOperationException("Identidade da conexão recusada.");
        }
        // Metadata for the connected account only. No authentication strings are read.
        var grantee = "'" + user + "'@'localhost'";
        foreach (var table in new[] { "USER_PRIVILEGES", "SCHEMA_PRIVILEGES", "COLUMN_PRIVILEGES" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM information_schema.{table} WHERE GRANTEE=@grantee" +
                (table == "USER_PRIVILEGES" ? " AND PRIVILEGE_TYPE <> 'USAGE'" : "");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@grantee";
            parameter.Value = grantee;
            command.Parameters.Add(parameter);
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Privilégios adicionais recusados.");
        }
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in new[] { "empresas", "usuarios", "administradores_gerais" })
            foreach (var privilege in new[] { "SELECT", "INSERT", "UPDATE" })
                expected.Add(database + "/" + table + "/" + privilege + "/NO");
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT TABLE_SCHEMA,TABLE_NAME,PRIVILEGE_TYPE,IS_GRANTABLE FROM information_schema.TABLE_PRIVILEGES WHERE GRANTEE=@grantee";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@grantee";
            parameter.Value = grantee;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync();
            var actual = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
                actual.Add(string.Join("/", reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            if (!expected.SetEquals(actual))
                throw new InvalidOperationException("Permissões de tabelas recusadas.");
        }
    }
}

public sealed class TestingWriteGuard : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        => throw new InvalidOperationException("Testing exige gravação assíncrona com verificação da conexão.");

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context ?? throw new InvalidOperationException();
        if (context.Database.GetDbConnection().State != ConnectionState.Open)
            await context.Database.OpenConnectionAsync(cancellationToken);
        await DatabaseSafety.VerifyOpenAsync(context.Database.GetDbConnection(), true);
        if (context.ChangeTracker.Entries().Any(e => e.State == EntityState.Deleted))
            throw new InvalidOperationException("Exclusões proibidas nesta execução de testes.");
        return result;
    }
}
