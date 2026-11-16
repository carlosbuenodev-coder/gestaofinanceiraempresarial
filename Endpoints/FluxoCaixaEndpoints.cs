using System.Security.Claims;
using GestaoFinanceira.Models;
using GestaoFinanceira.Services;

namespace GestaoFinanceira.Endpoints;

public static class FluxoCaixaEndpoints
{
    public static void MapFluxoCaixa(this WebApplication app)
    {
        var grupo = app.MapGroup("/fluxo-caixa").RequireAuthorization();
        foreach (var consulta in new[] { "resumo", "previsto", "realizado", "por-conta", "por-periodo", "dashboard" })
        {
            var nome = consulta;
            grupo.MapGet("/" + nome, async ([AsParameters] FluxoCaixaQuery query, ClaimsPrincipal principal,
                FluxoCaixaService service, CancellationToken ct) => await service.ConsultarAsync(nome, query, principal, ct))
                .WithName("FluxoCaixa-" + nome);
        }
    }
}
