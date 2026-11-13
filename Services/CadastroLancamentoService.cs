using System.Security.Claims;
using System.Text.Json;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Services;

public sealed class CadastroLancamentoService(GestaoFinanceiraContext db, IHostEnvironment environment)
{
    public async Task<IResult> CriarAsync(CriarLancamentoRequest request, ClaimsPrincipal principal, CancellationToken ct)
    {
        // This delivery permits financial writes exclusively in the isolated Testing database.
        if (!environment.IsEnvironment("Testing"))
            return Results.Problem("Cadastro financeiro disponível somente em Testing nesta entrega.", statusCode: 403);
        var perfil = principal.FindFirstValue(ClaimTypes.Role);
        if (perfil is not ("ADMIN" or "FINANCEIRO" or "ADMIN_GERAL")) return Results.Forbid();
        if (perfil != "ADMIN_GERAL" && request.EmpresaId.HasValue)
            return Results.BadRequest(new { mensagem = "Usuário empresarial não pode informar empresaId." });
        var erros = request.Validar();
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        await DatabaseSafety.VerifyAsync(db.Database.GetConnectionString()!, true);
        await db.Database.OpenConnectionAsync(ct);
        await DatabaseSafety.VerifyOpenAsync(db.Database.GetDbConnection(), true);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var escopo = await EscopoFinanceiro.ResolverAsync(principal, request.EmpresaId, db, ct);
        if (escopo.Erro is not null) return escopo.Erro;
        var empresa = escopo.EmpresaId;
        var categoria = await db.Set<CategoriaFinanceira>().AsNoTracking()
            .SingleOrDefaultAsync(c => c.EmpresaId == empresa && c.Id == request.CategoriaId, ct);
        if (categoria is null || categoria.Tipo != request.Tipo)
            erros[nameof(request.CategoriaId)] = ["Categoria inválida para a empresa e o tipo informados."];
        async Task ValidarReferencia<T>(long? id, string campo) where T : class, IReferenciaFinanceira
        {
            if (id.HasValue && !await db.Set<T>().AsNoTracking().AnyAsync(x => x.EmpresaId == empresa && x.Id == id, ct))
                erros[campo] = ["Referência inválida para a empresa."];
        }
        await ValidarReferencia<ClienteFinanceiro>(request.ClienteId, nameof(request.ClienteId));
        await ValidarReferencia<FornecedorFinanceiro>(request.FornecedorId, nameof(request.FornecedorId));
        await ValidarReferencia<CentroCustoFinanceiro>(request.CentroCustoId, nameof(request.CentroCustoId));
        if (erros.Count > 0) return Results.ValidationProblem(erros);
        var item = new Lancamento
        {
            EmpresaId = empresa, Tipo = request.Tipo!, Descricao = request.Descricao!.Trim(),
            CategoriaId = request.CategoriaId!.Value, ClienteId = request.ClienteId,
            FornecedorId = request.FornecedorId, CentroCustoId = request.CentroCustoId,
            DataEmissao = request.DataEmissao!.Value.ToDateTime(TimeOnly.MinValue),
            DataVencimento = request.DataVencimento!.Value.ToDateTime(TimeOnly.MinValue),
            Valor = request.Valor!.Value, Observacoes = request.Observacoes, Status = "ABERTO"
        };
        try
        {
            db.Lancamentos.Add(item);
            await db.SaveChangesAsync(ct);
            var autorId = long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")!);
            var autorTipo = perfil == "ADMIN_GERAL" ? "ADMIN_GERAL" : "EMPRESA";
            db.Set<Auditoria>().Add(new Auditoria
            {
                EmpresaId = empresa, UsuarioId = autorTipo == "EMPRESA" ? autorId : null,
                Entidade = "lancamentos", RegistroId = item.Id, Acao = "CRIAR", CriadoEm = DateTime.UtcNow,
                Detalhes = JsonSerializer.Serialize(new { autorId, autorTipo, perfil })
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Results.Problem("Não foi possível registrar lançamento e auditoria. Nenhuma gravação foi confirmada.", statusCode: 500);
        }
        var response = new LancamentoResponse(item.Id, item.EmpresaId, item.Tipo, item.Descricao,
            item.ClienteId, item.FornecedorId, item.CategoriaId, item.CentroCustoId, item.DataEmissao,
            item.DataVencimento, item.Valor, item.Status, item.Observacoes, item.CriadoEm);
        var location = $"/lancamentos/{item.Id}" + (perfil == "ADMIN_GERAL" ? $"?empresaId={empresa}" : "");
        return Results.Created(location, response);
    }
}
