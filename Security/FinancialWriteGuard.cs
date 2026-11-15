using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using GestaoFinanceira.Data;

namespace GestaoFinanceira.Security;

public sealed class FinancialWriteGuard(string environmentName) : SaveChangesInterceptor
{
    private static bool HasFinancialChanges(DbContext? db) => db is not null && db.ChangeTracker.Entries()
        .Any(e => (e.Entity is Lancamento or Auditoria or BaixaFinanceira or EstornoBaixa) && (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (HasFinancialChanges(eventData.Context)) throw new InvalidOperationException("Gravação financeira exige verificação assíncrona do ambiente e da conexão.");
        return result;
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!HasFinancialChanges(eventData.Context)) return result;
        var testing = environmentName == "Testing";
        var db = eventData.Context!;
        var movimento = testing && db is GestaoFinanceiraContext { OperacaoBaixa: true } &&
            db.Database.CurrentTransaction is not null;
        if (db.ChangeTracker.Entries().Any(e => (e.Entity is BaixaFinanceira or EstornoBaixa) &&
            (e.State is EntityState.Modified or EntityState.Deleted || (e.State == EntityState.Added && !movimento))))
            throw new InvalidOperationException("Baixas e estornos são imutáveis; inclusão somente na transação de baixas em Testing.");
        if (!testing && environmentName != "Development")
            throw new InvalidOperationException("Gravação financeira permitida apenas em Development e Testing.");
        // Reject forbidden changes before opening any connection. Audit is append-only.
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is Lancamento or Auditoria))
        {
            if (entry.State == EntityState.Deleted ||
                (entry.State == EntityState.Modified &&
                    (!FinancialUpdatePolicy.AllowsUpdates(environmentName) || entry.Entity is Auditoria)))
                throw new InvalidOperationException("Somente alterações financeiras expressamente habilitadas; exclusão e alteração de auditoria são proibidas.");
            if (entry.State != EntityState.Modified) continue;
            if (movimento)
            {
                var statusMovimento = entry.Property(nameof(Lancamento.Status));
                if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(Lancamento.Status)) ||
                    statusMovimento.OriginalValue is not ("ABERTO" or "PARCIAL" or "QUITADO") ||
                    statusMovimento.CurrentValue is not ("ABERTO" or "PARCIAL" or "QUITADO"))
                    throw new InvalidOperationException("Baixa só pode recalcular status de lançamento não cancelado.");
                continue;
            }
            string[] editaveis = ["Descricao", "CategoriaId", "ClienteId", "FornecedorId", "CentroCustoId",
                "DataEmissao", "DataVencimento", "Valor", "Observacoes", "Status"];
            var status = entry.Property(nameof(Lancamento.Status));
            if (entry.Properties.Any(p => p.IsModified && !editaveis.Contains(p.Metadata.Name)) ||
                (string?)status.OriginalValue != "ABERTO" ||
                (status.IsModified && (string?)status.CurrentValue != "CANCELADO"))
                throw new InvalidOperationException("Somente campos editáveis de lançamentos ABERTO podem mudar em ambiente habilitado.");
        }
        await DatabaseSafety.VerifyAsync(db.Database.GetConnectionString()!, testing);
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync(cancellationToken);
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), testing);
        return result;
    }
}

// Registered exclusively in Testing with the explicit --security-tests command.
// No endpoint, request header or configuration key can arm this failure.
public sealed class AuditFailureInterceptor : SaveChangesInterceptor
{
    private int failNext;
    public int Failures { get; private set; }
    public void Arm() => Interlocked.Exchange(ref failNext, 1);
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Auditoria>().Any(e => e.State == EntityState.Added) &&
            Interlocked.Exchange(ref failNext, 0) == 1)
        {
            Failures++;
            throw new DbUpdateException("Falha de auditoria simulada exclusivamente pelo cenário de testes.");
        }
        return ValueTask.FromResult(result);
    }
}
