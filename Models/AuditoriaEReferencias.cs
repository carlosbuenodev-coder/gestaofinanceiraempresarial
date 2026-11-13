namespace GestaoFinanceira.Models;

public interface IReferenciaFinanceira
{
    long Id { get; set; }
    long EmpresaId { get; set; }
}
public sealed class CategoriaFinanceira : IReferenciaFinanceira
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
    public string Tipo { get; set; } = "";
}
public sealed class ClienteFinanceiro : IReferenciaFinanceira
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
}
public sealed class FornecedorFinanceiro : IReferenciaFinanceira
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
}
public sealed class CentroCustoFinanceiro : IReferenciaFinanceira
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
}
public sealed class Auditoria
{
    public long Id { get; set; }
    public long EmpresaId { get; set; }
    public long? UsuarioId { get; set; }
    public string Entidade { get; set; } = "";
    public long RegistroId { get; set; }
    public string Acao { get; set; } = "";
    public string Detalhes { get; set; } = "{}";
    public DateTime? CriadoEm { get; set; }
}
