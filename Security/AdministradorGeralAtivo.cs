using System.Security.Claims;
using GestaoFinanceira.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Security;

public sealed class AdministradorGeralAtivo : IAuthorizationRequirement
{
    public const string Politica = "SomenteAdminGeral";

    public static Task<bool> VerificarAsync(ClaimsPrincipal principal, GestaoFinanceiraContext db,
        CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !principal.IsInRole("ADMIN_GERAL") ||
            principal.HasClaim(c => c.Type == "empresa_id" || c.Type == "tipo_usuario") ||
            !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub"), out var id) || id <= 0)
            return Task.FromResult(false);

        return db.AdministradoresGerais.AsNoTracking().AnyAsync(a => a.Id == id && a.Ativo, ct);
    }
}

public sealed class AdministradorGeralAtivoHandler(GestaoFinanceiraContext db)
    : AuthorizationHandler<AdministradorGeralAtivo>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        AdministradorGeralAtivo requirement)
    {
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await AdministradorGeralAtivo.VerificarAsync(context.User, db, ct))
            context.Succeed(requirement);
    }
}
