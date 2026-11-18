using System.Security.Claims;
using GestaoFinanceira.Data;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Security;

public static class EscopoFinanceiro
{
    public static async Task<(long EmpresaId, IResult? Erro)> ResolverAsync(
        ClaimsPrincipal principal, long? empresaSolicitada, GestaoFinanceiraContext db, CancellationToken ct)
    {
        if (!long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub"), out var id) || id <= 0)
            return (0, Results.Forbid());
        var perfil = principal.FindFirstValue(ClaimTypes.Role);
        long empresa;
        if (perfil == "ADMIN_GERAL")
        {
            if (!await AdministradorGeralAtivo.VerificarAsync(principal, db, ct))
                return (0, Results.Forbid());
            if (empresaSolicitada is null)
                return (0, Results.BadRequest(new { mensagem = "ADMIN_GERAL exige empresaId explícito." }));
            empresa = empresaSolicitada.Value;
        }
        else
        {
            if (perfil is not ("ADMIN" or "FINANCEIRO" or "CONSULTA") ||
                principal.FindFirstValue("tipo_usuario") != "EMPRESA" ||
                !long.TryParse(principal.FindFirstValue("empresa_id"), out empresa) || empresa <= 0 ||
                !await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == id && u.EmpresaId == empresa &&
                    u.Ativo && u.Perfil == perfil, ct))
                return (0, Results.Forbid());
            if (empresaSolicitada.HasValue && empresaSolicitada != empresa)
                return (0, Results.Forbid());
        }
        if (!await db.Empresas.AsNoTracking().AnyAsync(e => e.Id == empresa && e.Ativo, ct))
            return (0, Results.Forbid());
        return (empresa, null);
    }
}
