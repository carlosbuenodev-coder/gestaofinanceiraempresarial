
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("GestaoFinanceira");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "A conexão GestaoFinanceira não foi configurada.");
}

builder.Services.AddDbContext<GestaoFinanceiraContext>(options =>
{
    options.UseMySQL(connectionString);
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoint original de previsão do tempo
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast");

// Teste de conexão
app.MapGet("/teste-conexao", async () =>
{
    try
    {
        await using var conexao =
            new MySqlConnection(connectionString);

        await conexao.OpenAsync();

        return Results.Ok(new
        {
            mensagem = "Conexão com MySQL realizada com sucesso!"
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "Não foi possível conectar ao MySQL.",
            statusCode: 500);
    }
});

// GET - Listar empresas
app.MapGet("/empresas", async (
    GestaoFinanceiraContext contexto) =>
{
    var empresas = await contexto.Empresas
        .AsNoTracking()
        .OrderBy(e => e.Id)
        .ToListAsync();

    return Results.Ok(empresas);
})
.WithName("ListarEmpresas");

// GET - Buscar empresa pelo ID
app.MapGet("/empresas/{id:long}", async (
    long id,
    GestaoFinanceiraContext contexto) =>
{
    var empresa = await contexto.Empresas
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id);

    if (empresa is null)
    {
        return Results.NotFound(new
        {
            mensagem = "Empresa não encontrada."
        });
    }

    return Results.Ok(empresa);
})
.WithName("BuscarEmpresaPorId");

// POST - Cadastrar empresa
app.MapPost("/empresas", async (
    CriarEmpresaRequest request,
    GestaoFinanceiraContext contexto) =>
{
    var erros = ValidarEmpresa(
        request.RazaoSocial,
        request.NomeFantasia,
        request.Cnpj,
        request.Email,
        request.Telefone);

    if (erros.Count > 0)
    {
        return Results.ValidationProblem(erros);
    }

    var cnpj = NormalizarOpcional(request.Cnpj);

    if (cnpj is not null &&
        await contexto.Empresas
            .AsNoTracking()
            .AnyAsync(e => e.Cnpj == cnpj))
    {
        return Results.Conflict(new
        {
            mensagem = "Já existe uma empresa com este CNPJ."
        });
    }

    var empresa = new Empresa
    {
        RazaoSocial = request.RazaoSocial.Trim(),
        NomeFantasia = NormalizarOpcional(request.NomeFantasia),
        Cnpj = cnpj,
        Email = NormalizarOpcional(request.Email),
        Telefone = NormalizarOpcional(request.Telefone),
        Ativo = request.Ativo
    };

    contexto.Empresas.Add(empresa);

    try
    {
        await contexto.SaveChangesAsync();
    }
    catch (DbUpdateException ex)
        when (ErroDeChaveDuplicada(ex))
    {
        return Results.Conflict(new
        {
            mensagem = "Já existe uma empresa com este CNPJ."
        });
    }

    return Results.Created(
        $"/empresas/{empresa.Id}",
        empresa);
})
.WithName("CadastrarEmpresa");

// PUT - Atualizar empresa existente
app.MapPut("/empresas/{id:long}", async (
    long id,
    AtualizarEmpresaRequest request,
    GestaoFinanceiraContext contexto) =>
{
    if (id <= 0)
    {
        return Results.BadRequest(new
        {
            mensagem = "O ID deve ser maior que zero."
        });
    }

    var erros = ValidarEmpresa(
        request.RazaoSocial,
        request.NomeFantasia,
        request.Cnpj,
        request.Email,
        request.Telefone);

    if (erros.Count > 0)
    {
        return Results.ValidationProblem(erros);
    }

    var empresa = await contexto.Empresas
        .FirstOrDefaultAsync(e => e.Id == id);

    if (empresa is null)
    {
        return Results.NotFound(new
        {
            mensagem = "Empresa não encontrada."
        });
    }

    var cnpj = NormalizarOpcional(request.Cnpj);

    // Verifica se outra empresa já utiliza o CNPJ
    if (cnpj is not null &&
        await contexto.Empresas
            .AsNoTracking()
            .AnyAsync(e => e.Cnpj == cnpj && e.Id != id))
    {
        return Results.Conflict(new
        {
            mensagem = "Este CNPJ pertence a outra empresa."
        });
    }

    // Atualiza somente os campos permitidos
    empresa.RazaoSocial = request.RazaoSocial.Trim();
    empresa.NomeFantasia =
        NormalizarOpcional(request.NomeFantasia);
    empresa.Cnpj = cnpj;
    empresa.Email = NormalizarOpcional(request.Email);
    empresa.Telefone = NormalizarOpcional(request.Telefone);
    empresa.Ativo = request.Ativo;

    // ID e CriadoEm não são alterados

    try
    {
        await contexto.SaveChangesAsync();
    }
    catch (DbUpdateException ex)
        when (ErroDeChaveDuplicada(ex))
    {
        return Results.Conflict(new
        {
            mensagem = "Este CNPJ pertence a outra empresa."
        });
    }

    return Results.Ok(empresa);
})
.WithName("AtualizarEmpresa");

app.Run();

// Remove espaços e transforma texto vazio em null
static string? NormalizarOpcional(string? valor)
{
    return string.IsNullOrWhiteSpace(valor)
        ? null
        : valor.Trim();
}

// Valida os dados usados no POST e no PUT
static Dictionary<string, string[]> ValidarEmpresa(
    string? razaoSocial,
    string? nomeFantasia,
    string? cnpj,
    string? email,
    string? telefone)
{
    var erros = new Dictionary<string, string[]>();

    razaoSocial = razaoSocial?.Trim();
    nomeFantasia = NormalizarOpcional(nomeFantasia);
    cnpj = NormalizarOpcional(cnpj);
    email = NormalizarOpcional(email);
    telefone = NormalizarOpcional(telefone);

    if (string.IsNullOrWhiteSpace(razaoSocial))
    {
        erros["razaoSocial"] = new[]
        {
            "A razão social é obrigatória."
        };
    }
    else if (razaoSocial.Length > 180)
    {
        erros["razaoSocial"] = new[]
        {
            "A razão social deve ter no máximo 180 caracteres."
        };
    }

    if (nomeFantasia?.Length > 180)
    {
        erros["nomeFantasia"] = new[]
        {
            "O nome fantasia deve ter no máximo 180 caracteres."
        };
    }

    if (cnpj is not null)
    {
        if (cnpj.Length != 14 ||
            !cnpj.All(char.IsAsciiDigit))
        {
            erros["cnpj"] = new[]
            {
                "O CNPJ deve conter 14 dígitos numéricos."
            };
        }
        else if (!CnpjValido(cnpj))
        {
            erros["cnpj"] = new[]
            {
                "O CNPJ informado é inválido."
            };
        }
    }

    if (email is not null)
    {
        if (email.Length > 150)
        {
            erros["email"] = new[]
            {
                "O e-mail deve ter no máximo 150 caracteres."
            };
        }
        else if (!new EmailAddressAttribute().IsValid(email))
        {
            erros["email"] = new[]
            {
                "O e-mail informado é inválido."
            };
        }
    }

    if (telefone?.Length > 25)
    {
        erros["telefone"] = new[]
        {
            "O telefone deve ter no máximo 25 caracteres."
        };
    }

    return erros;
}

// Verifica os dígitos do CNPJ numérico
static bool CnpjValido(string cnpj)
{
    if (cnpj.Length != 14 ||
        !cnpj.All(char.IsAsciiDigit) ||
        cnpj.Distinct().Count() == 1)
    {
        return false;
    }

    int CalcularDigito(string numeros, int[] pesos)
    {
        var soma = 0;

        for (var i = 0; i < pesos.Length; i++)
        {
            soma += (numeros[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    var pesosPrimeiro = new[]
    {
        5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2
    };

    var pesosSegundo = new[]
    {
        6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2
    };

    var primeiro = CalcularDigito(
        cnpj[..12], pesosPrimeiro);

    var segundo = CalcularDigito(
        cnpj[..12] + primeiro, pesosSegundo);

    return cnpj[12] - '0' == primeiro &&
           cnpj[13] - '0' == segundo;
}

// Identifica violação de chave única no MySQL
static bool ErroDeChaveDuplicada(DbUpdateException ex)
{
    var erro = ex.InnerException;

    while (erro is not null)
    {
        if (erro is MySqlException mysql &&
            mysql.Number == 1062)
        {
            return true;
        }

        erro = erro.InnerException;
    }

    return false;
}

record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}
