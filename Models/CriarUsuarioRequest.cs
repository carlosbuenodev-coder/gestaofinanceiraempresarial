using System.ComponentModel.DataAnnotations;

namespace GestaoFinanceira.Models
{
    public class CriarUsuarioRequest
    {
        [Required]
        public long EmpresaId { get; set; }

        [Required]
        [MaxLength(120)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Senha { get; set; } = string.Empty;

        [Required]
        public string Perfil { get; set; } = "CONSULTA";
    }
}