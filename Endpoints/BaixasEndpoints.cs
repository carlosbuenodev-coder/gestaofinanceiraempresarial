using System.Security.Claims;
using GestaoFinanceira.Models;
using GestaoFinanceira.Services;

namespace GestaoFinanceira.Endpoints;

public static class BaixasEndpoints
{
    public static void MapBaixas(this WebApplication app)
    {
        app.MapPost("/lancamentos/{id:long}/baixas", async (long id, CriarBaixaRequest request,
            HttpRequest http, ClaimsPrincipal principal, BaixasService service, CancellationToken ct) =>
            http.Query.ContainsKey("empresaId") ? Results.BadRequest(new { mensagem = "Informe empresaId somente no JSON." }) :
            await service.CriarAsync(id, request, principal, ct)).RequireAuthorization().WithName("CriarBaixa");
        app.MapPost("/baixas/{id:long}/estorno", async (long id, EstornarBaixaRequest request,
            HttpRequest http, ClaimsPrincipal principal, BaixasService service, CancellationToken ct) =>
            http.Query.ContainsKey("empresaId") ? Results.BadRequest(new { mensagem = "Informe empresaId somente no JSON." }) :
            await service.EstornarAsync(id, request, principal, ct)).RequireAuthorization().WithName("EstornarBaixa");
        app.MapGet("/lancamentos/{id:long}/baixas", async (long id, long? empresaId, int? pagina,
            int? tamanhoPagina, ClaimsPrincipal principal, BaixasService service, CancellationToken ct) =>
            await service.ConsultarAsync("lista", id, empresaId, pagina ?? 1, tamanhoPagina ?? 20, principal, ct))
            .RequireAuthorization().WithName("ListarBaixas");
        app.MapGet("/lancamentos/{id:long}/saldo", async (long id, long? empresaId, ClaimsPrincipal principal,
            BaixasService service, CancellationToken ct) => await service.ConsultarAsync("saldo", id, empresaId, 1, 20, principal, ct))
            .RequireAuthorization().WithName("SaldoLancamento");
        app.MapGet("/baixas/{id:long}", async (long id, long? empresaId, ClaimsPrincipal principal,
            BaixasService service, CancellationToken ct) => await service.ConsultarAsync("baixa", id, empresaId, 1, 20, principal, ct))
            .RequireAuthorization().WithName("BuscarBaixa");
    }
}
