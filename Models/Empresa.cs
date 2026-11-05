namespace GestaoFinanceira.Models
{
    public class Empresa
    {
        public long Id { get; set; }

        public string RazaoSocial { get; set; } = string.Empty;

        public string? NomeFantasia { get; set; }

        public string? Cnpj { get; set; }

        public string? Email { get; set; }

        public string? Telefone { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime? CriadoEm { get; set; }
    }
}