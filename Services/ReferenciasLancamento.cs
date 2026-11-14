using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Services;

public static class ReferenciasLancamento
{
    public static async Task ValidarAsync(GestaoFinanceiraContext db, CriarLancamentoRequest request,
        long empresa, Dictionary<string, string[]> erros, CancellationToken ct)
    {
        var categoria = await db.Set<CategoriaFinanceira>().AsNoTracking()
            .SingleOrDefaultAsync(c => c.EmpresaId == empresa && c.Id == request.CategoriaId, ct);
        if (categoria is null || categoria.Tipo != request.Tipo)
            erros[nameof(request.CategoriaId)] = ["Categoria inválida para a empresa e o tipo informados."];
        async Task Validar<T>(long? id, string campo) where T : class, IReferenciaFinanceira
        {
            if (id.HasValue && !await db.Set<T>().AsNoTracking().AnyAsync(x => x.EmpresaId == empresa && x.Id == id, ct))
                erros[campo] = ["Referência inválida para a empresa."];
        }
        await Validar<ClienteFinanceiro>(request.ClienteId, nameof(request.ClienteId));
        await Validar<FornecedorFinanceiro>(request.FornecedorId, nameof(request.FornecedorId));
        await Validar<CentroCustoFinanceiro>(request.CentroCustoId, nameof(request.CentroCustoId));
    }
}
