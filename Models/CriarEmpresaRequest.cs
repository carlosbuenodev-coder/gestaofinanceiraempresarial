
namespace GestaoFinanceira.Models
{
    public class CriarEmpresaRequest
    {
        public string RazaoSocial { get; set; } = string.Empty;

        public string? NomeFantasia { get; set; }

        public string? Cnpj { get; set; }

        public string? Email { get; set; }

        public string? Telefone { get; set; }

        public bool Ativo { get; set; } = true;
    }
}
