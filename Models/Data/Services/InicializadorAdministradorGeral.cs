
using GestaoFinanceira.Data;
using GestaoFinanceira.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GestaoFinanceira.Services;

public static class InicializadorAdministradorGeral
{
    public static async Task ExecutarAsync(
        IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var contexto = scope.ServiceProvider
            .GetRequiredService<GestaoFinanceiraContext>();

        var jaExiste = await contexto.AdministradoresGerais
            .AnyAsync();

        if (jaExiste)
        {
            Console.WriteLine(
                "Já existe um administrador geral cadastrado.");
            return;
        }

        Console.Write("Nome do administrador geral: ");
        var nome = Console.ReadLine()?.Trim();

        Console.Write("E-mail: ");
        var email = Console.ReadLine()?.Trim();

        Console.Write("Senha: ");
        var senha = LerSenhaOculta();

        if (string.IsNullOrWhiteSpace(nome) ||
            nome.Length > 120 ||
            string.IsNullOrWhiteSpace(email) ||
            email.Length > 150 ||
            !new EmailAddressAttribute().IsValid(email) ||
            senha.Length < 12)
        {
            Console.WriteLine(
                "Dados inválidos. Use nome e e-mail válidos e senha com pelo menos 12 caracteres.");
            return;
        }

        var administrador = new AdministradorGeral
        {
            Nome = nome,
            Email = email,
            Ativo = true
        };

        var hasher = new PasswordHasher<AdministradorGeral>();

        administrador.SenhaHash =
            hasher.HashPassword(administrador, senha);

        contexto.AdministradoresGerais.Add(administrador);

        await contexto.SaveChangesAsync();

        Console.WriteLine(
            "Administrador geral cadastrado com sucesso.");
    }

    private static string LerSenhaOculta()
    {
        var caracteres = new List<char>();

        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);

            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (caracteres.Count > 0)
                {
                    caracteres.RemoveAt(caracteres.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(tecla.KeyChar))
            {
                caracteres.Add(tecla.KeyChar);
            }
        }

        return new string(caracteres.ToArray());
    }
}
