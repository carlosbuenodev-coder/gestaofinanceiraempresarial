namespace GestaoFinanceira.Models
{
    public class Usuario
    {
        public long Id { get; set; }

        public long EmpresaId { get; set; }

        public string Nome { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string SenhaHash { get; set; } = string.Empty;

        public string Perfil { get; set; } = "CONSULTA";

        public bool Ativo { get; set; } = true;
    }
}