using System.Security.Claims;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Security;
using GestaoFinanceira.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GestaoFinanceira.Tests;

public sealed class AutorizacaoFinanceiraTests
{
    [Theory]
    [InlineData("ADMIN")]
    [InlineData("ADMIN_GERAL")]
    public async Task VerificarAdminGeral_RejeitaIdentidadeEmpresarialAntesDeConsultar(string perfil)
    {
        await using var contexto = CriarContextoSemProvider();
        var principal = IdentidadeEmpresarial(perfil);

        var autorizado = await AdministradorGeralAtivo.VerificarAsync(principal, contexto, TestContext.Current.CancellationToken);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task CriarLancamento_AdminEmpresarialComEmpresaExplicita_RetornaBadRequest()
    {
        await using var contexto = CriarContextoSemProvider();
        var service = new CadastroLancamentoService(contexto, new AmbienteTesting());
        var data = new DateOnly(2026, 11, 18);
        var request = new CriarLancamentoRequest(
            Tipo: "RECEITA", Descricao: "Lançamento de teste", CategoriaId: 1,
            DataEmissao: data, DataVencimento: data, Valor: 10.50m, EmpresaId: 40);
        Assert.Empty(request.Validar());

        var resultado = await service.CriarAsync(request, IdentidadeEmpresarial("ADMIN"), TestContext.Current.CancellationToken);

        var resposta = Assert.IsAssignableFrom<IStatusCodeHttpResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, resposta.StatusCode);
    }

    private static GestaoFinanceiraContext CriarContextoSemProvider() =>
        new(new DbContextOptionsBuilder<GestaoFinanceiraContext>().Options);

    private static ClaimsPrincipal IdentidadeEmpresarial(string perfil) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, perfil),
                new Claim("empresa_id", "39")
            ], "Teste"));

    private sealed class AmbienteTesting : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "GestaoFinanceira.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
