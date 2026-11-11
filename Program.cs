using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using GestaoFinanceira.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

// A chave JWT permanece no User Secrets; nunca no repositório.
var chaveJwtBase64 = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(chaveJwtBase64))
{
    throw new InvalidOperationException("A chave Jwt:Key não foi configurada.");
}

byte[] chaveJwt;
try
{
    chaveJwt = Convert.FromBase64String(chaveJwtBase64);
}
catch (FormatException)
{
    throw new InvalidOperationException("A chave Jwt:Key deve estar em Base64.");
}

if (chaveJwt.Length < 32)
{
    throw new InvalidOperationException("A chave Jwt:Key deve ter pelo menos 32 bytes.");
}

const string emissorJwt = "GestaoFinanceiraAPI";
const string publicoJwt = "GestaoFinanceiraAPI";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = emissorJwt,
            ValidateAudience = true,
            ValidAudience = publicoJwt,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(chaveJwt),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SomenteAdminGeral", politica =>
        politica.RequireAuthenticatedUser()
                .RequireRole("ADMIN_GERAL"))
    .AddPolicy("UsuarioEmpresa", politica =>
        politica.RequireAuthenticatedUser()
                .RequireRole("ADMIN", "FINANCEIRO", "CONSULTA"))
    .AddPolicy("AdminEmpresa", politica =>
        politica.RequireAuthenticatedUser()
                .RequireRole("ADMIN"));

builder.Services.AddOpenApi();

var app = builder.Build();

// Cadastro inicial do administrador geral:
// executado somente por comando explícito e em ambiente Development.
if (args.Contains("--criar-admin-geral"))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine(
            "Cadastro inicial permitido somente em Development.");
        return;
    }

    await InicializadorAdministradorGeral.ExecutarAsync(
        app.Services);

    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Login público: valida a senha sem devolver hash ou outros dados sensíveis.
app.MapPost("/auth/admin/login", async (
    LoginAdministradorRequest request,
    GestaoFinanceiraContext contexto) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrEmpty(request.Senha))
    {
        return Results.BadRequest(new { mensagem = "Informe e-mail e senha." });
    }

    var email = request.Email.Trim();
    var administrador = await contexto.AdministradoresGerais
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.Email == email && a.Ativo);

    if (administrador is null)
    {
        return Results.Unauthorized();
    }

    var verificador = new PasswordHasher<AdministradorGeral>();
    var resultado = verificador.VerifyHashedPassword(
        administrador, administrador.SenhaHash, request.Senha);

    if (resultado == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    var agora = DateTime.UtcNow;
    var expiraEm = agora.AddHours(1);
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, administrador.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, administrador.Email),
        new Claim(ClaimTypes.Role, "ADMIN_GERAL"),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var credenciais = new SigningCredentials(
        new SymmetricSecurityKey(chaveJwt),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: emissorJwt,
        audience: publicoJwt,
        claims: claims,
        notBefore: agora,
        expires: expiraEm,
        signingCredentials: credenciais);

    return Results.Ok(new
    {
        token = new JwtSecurityTokenHandler().WriteToken(token),
        tipo = "Bearer",
        expiraEmUtc = expiraEm
    });
})
.AllowAnonymous()
.WithName("LoginAdministradorGeral");

// Login de usuário de empresa: a empresa faz parte da identidade de acesso.
// Credenciais inválidas, empresa inativa ou usuário inativo recebem 401.
app.MapPost("/auth/usuarios/login", async (
    LoginUsuarioEmpresaRequest request,
    GestaoFinanceiraContext contexto) =>
{
    if (request.EmpresaId <= 0 ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrEmpty(request.Senha))
        return Results.Unauthorized();

    var email = request.Email.Trim();

    var usuario = await contexto.Usuarios.AsNoTracking()
        .FirstOrDefaultAsync(u =>
            u.EmpresaId == request.EmpresaId &&
            u.Email == email &&
            u.Ativo);

    if (usuario is null)
        return Results.Unauthorized();

    var empresaAtiva = await contexto.Empresas.AsNoTracking()
        .AnyAsync(e => e.Id == usuario.EmpresaId && e.Ativo);

    if (!empresaAtiva)
        return Results.Unauthorized();

    var verificador = new PasswordHasher<Usuario>();
    var resultado = verificador.VerifyHashedPassword(
        usuario, usuario.SenhaHash, request.Senha);

    if (resultado == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    if (usuario.Perfil is not ("ADMIN" or "FINANCEIRO" or "CONSULTA"))
        return Results.Unauthorized();

    var agora = DateTime.UtcNow;
    var expiraEm = agora.AddHours(1);

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
        new Claim(ClaimTypes.Role, usuario.Perfil),
        new Claim("empresa_id", usuario.EmpresaId.ToString()),
        new Claim("tipo_usuario", "EMPRESA"),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(
        issuer: emissorJwt,
        audience: publicoJwt,
        claims: claims,
        notBefore: agora,
        expires: expiraEm,
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(chaveJwt),
            SecurityAlgorithms.HmacSha256));

    return Results.Ok(new
    {
        token = new JwtSecurityTokenHandler().WriteToken(token),
        tipo = "Bearer",
        expiraEmUtc = expiraEm,
        usuario = new UsuarioResponse
        {
            Id = usuario.Id,
            EmpresaId = usuario.EmpresaId,
            Nome = usuario.Nome,
            Email = usuario.Email,
            Perfil = usuario.Perfil,
            Ativo = usuario.Ativo
        }
    });
})
.AllowAnonymous()
.WithName("LoginUsuarioEmpresa");

// O usuário consulta apenas seu próprio cadastro.
// Os IDs são retirados do JWT, nunca de parâmetros enviados pelo cliente.
// Também conferimos a situação atual do usuário e da empresa no banco.
app.MapGet("/usuarios/me", async (
    ClaimsPrincipal principal,
    GestaoFinanceiraContext contexto) =>
{
    var idTexto = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var empresaTexto = principal.FindFirstValue("empresa_id");

    if (!long.TryParse(idTexto, out var usuarioId) ||
        !long.TryParse(empresaTexto, out var empresaId) ||
        principal.FindFirstValue("tipo_usuario") != "EMPRESA")
        return Results.Forbid();

    var usuario = await contexto.Usuarios.AsNoTracking()
        .FirstOrDefaultAsync(u =>
            u.Id == usuarioId && u.EmpresaId == empresaId && u.Ativo);

    if (usuario is null ||
        !await contexto.Empresas.AsNoTracking()
            .AnyAsync(e => e.Id == empresaId && e.Ativo) ||
        usuario.Perfil != principal.FindFirstValue(ClaimTypes.Role))
        return Results.Forbid();

    return Results.Ok(new UsuarioResponse
    {
        Id = usuario.Id,
        EmpresaId = usuario.EmpresaId,
        Nome = usuario.Nome,
        Email = usuario.Email,
        Perfil = usuario.Perfil,
        Ativo = usuario.Ativo
    });
})
.WithName("MeuUsuario")
.RequireAuthorization("UsuarioEmpresa");

// ADMIN da empresa lista apenas os usuários da empresa indicada no JWT.
// O administrador geral continua separado e não usa este endpoint.
app.MapGet("/usuarios/minha-empresa", async (
    ClaimsPrincipal principal,
    GestaoFinanceiraContext contexto) =>
{
    var idTexto = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var empresaTexto = principal.FindFirstValue("empresa_id");

    if (!long.TryParse(idTexto, out var usuarioId) ||
        !long.TryParse(empresaTexto, out var empresaId) ||
        principal.FindFirstValue("tipo_usuario") != "EMPRESA")
        return Results.Forbid();

    var adminValido = await contexto.Usuarios.AsNoTracking()
        .AnyAsync(u => u.Id == usuarioId &&
                       u.EmpresaId == empresaId &&
                       u.Ativo && u.Perfil == "ADMIN");

    var empresaAtiva = await contexto.Empresas.AsNoTracking()
        .AnyAsync(e => e.Id == empresaId && e.Ativo);

    if (!adminValido || !empresaAtiva)
        return Results.Forbid();

    var usuarios = await contexto.Usuarios.AsNoTracking()
        .Where(u => u.EmpresaId == empresaId)
        .OrderBy(u => u.Id)
        .Select(u => new UsuarioResponse
        {
            Id = u.Id,
            EmpresaId = u.EmpresaId,
            Nome = u.Nome,
            Email = u.Email,
            Perfil = u.Perfil,
            Ativo = u.Ativo
        })
        .ToListAsync();

    return Results.Ok(usuarios);
})
.WithName("ListarUsuariosMinhaEmpresa")
.RequireAuthorization("AdminEmpresa");

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
.WithName("ListarEmpresas")
.RequireAuthorization("SomenteAdminGeral");

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
.WithName("BuscarEmpresaPorId")
.RequireAuthorization("SomenteAdminGeral");

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
.WithName("CadastrarEmpresa")
.RequireAuthorization("SomenteAdminGeral");

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
.WithName("AtualizarEmpresa")
.RequireAuthorization("SomenteAdminGeral");

// POST - Cadastrar usuário de uma empresa (somente administrador geral)
app.MapPost("/usuarios", async (
    CriarUsuarioRequest request,
    GestaoFinanceiraContext contexto) =>
{
    var erros = new Dictionary<string, string[]>();
    var nome = request.Nome?.Trim();
    var email = request.Email?.Trim();
    var perfil = request.Perfil?.Trim().ToUpperInvariant();

    if (request.EmpresaId <= 0)
        erros["empresaId"] = new[] { "Informe uma empresa válida." };

    if (string.IsNullOrWhiteSpace(nome) || nome.Length > 120)
        erros["nome"] = new[] { "O nome deve ter entre 1 e 120 caracteres." };

    if (string.IsNullOrWhiteSpace(email) || email.Length > 150 ||
        !new EmailAddressAttribute().IsValid(email))
        erros["email"] = new[] { "Informe um e-mail válido de até 150 caracteres." };

    if (string.IsNullOrEmpty(request.Senha) || request.Senha.Length < 12)
        erros["senha"] = new[] { "A senha deve ter pelo menos 12 caracteres." };

    if (perfil is not ("ADMIN" or "FINANCEIRO" or "CONSULTA"))
        erros["perfil"] = new[] { "Use ADMIN, FINANCEIRO ou CONSULTA." };

    if (erros.Count > 0)
        return Results.ValidationProblem(erros);

    var empresa = await contexto.Empresas.AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == request.EmpresaId);

    if (empresa is null)
        return Results.NotFound(new { mensagem = "Empresa não encontrada." });

    if (!empresa.Ativo)
        return Results.BadRequest(new { mensagem = "Não é permitido cadastrar usuários em empresa inativa." });

    if (await contexto.Usuarios.AsNoTracking().AnyAsync(u =>
        u.EmpresaId == request.EmpresaId && u.Email == email))
        return Results.Conflict(new { mensagem = "Este e-mail já está cadastrado nesta empresa." });

    var usuario = new Usuario
    {
        EmpresaId = request.EmpresaId,
        Nome = nome!,
        Email = email!,
        Perfil = perfil!,
        Ativo = true
    };

    var hasher = new PasswordHasher<Usuario>();
    usuario.SenhaHash = hasher.HashPassword(usuario, request.Senha);

    contexto.Usuarios.Add(usuario);
    try
    {
        await contexto.SaveChangesAsync();
    }
    catch (DbUpdateException ex) when (ErroDeChaveDuplicada(ex))
    {
        return Results.Conflict(new { mensagem = "Este e-mail já está cadastrado nesta empresa." });
    }

    var resposta = new UsuarioResponse
    {
        Id = usuario.Id,
        EmpresaId = usuario.EmpresaId,
        Nome = usuario.Nome,
        Email = usuario.Email,
        Perfil = usuario.Perfil,
        Ativo = usuario.Ativo
    };

    return Results.Created($"/usuarios/{usuario.Id}", resposta);
})
.WithName("CadastrarUsuario")
.RequireAuthorization("SomenteAdminGeral");

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

// DTO do login; não contém a entidade ou o hash.
record LoginAdministradorRequest(string Email, string Senha);


// Dados exigidos no login de usuário da empresa.
record LoginUsuarioEmpresaRequest(long EmpresaId, string Email, string Senha);
