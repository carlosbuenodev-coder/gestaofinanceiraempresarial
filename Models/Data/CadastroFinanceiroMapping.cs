using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Data;

public static class CadastroFinanceiroMapping
{
    public static void Aplicar(ModelBuilder model)
    {
        Referencia<CategoriaFinanceira>(model, "categorias");
        model.Entity<CategoriaFinanceira>().Property(x => x.Tipo).HasColumnName("tipo").HasColumnType("enum('RECEITA','DESPESA')");
        Referencia<ClienteFinanceiro>(model, "clientes");
        Referencia<FornecedorFinanceiro>(model, "fornecedores");
        Referencia<CentroCustoFinanceiro>(model, "centros_custo");
        var e = model.Entity<Auditoria>();
        e.ToTable("auditoria");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
        e.Property(x => x.UsuarioId).HasColumnName("usuario_id");
        e.Property(x => x.Entidade).HasColumnName("entidade").HasMaxLength(60);
        e.Property(x => x.RegistroId).HasColumnName("registro_id");
        e.Property(x => x.Acao).HasColumnName("acao").HasMaxLength(30);
        e.Property(x => x.Detalhes).HasColumnName("detalhes").HasColumnType("json");
        e.Property(x => x.CriadoEm).HasColumnName("criado_em").HasColumnType("timestamp");
        e.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.NoAction);
    }
    private static void Referencia<T>(ModelBuilder model, string table) where T : class, IReferenciaFinanceira
    {
        var e = model.Entity<T>();
        e.ToTable(table);
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
    }
}
