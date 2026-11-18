using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Data;

public static class CadastroFinanceiroMapping
{
    public static void Aplicar(ModelBuilder model)
    {
        var baixa = model.Entity<BaixaFinanceira>();
        baixa.ToTable("baixas");
        baixa.HasKey(x => x.Id);
        baixa.Property(x => x.Id).HasColumnName("id");
        baixa.Property(x => x.LancamentoId).HasColumnName("lancamento_id");
        baixa.Property(x => x.ContaBancariaId).HasColumnName("conta_bancaria_id");
        baixa.Property(x => x.FormaPagamentoId).HasColumnName("forma_pagamento_id");
        baixa.Property(x => x.DataPagamento).HasColumnName("data_pagamento").HasColumnType("date");
        baixa.Property(x => x.Valor).HasColumnName("valor").HasPrecision(15, 2);
        baixa.Property(x => x.Juros).HasColumnName("juros").HasPrecision(15, 2);
        baixa.Property(x => x.Multa).HasColumnName("multa").HasPrecision(15, 2);
        baixa.Property(x => x.Desconto).HasColumnName("desconto").HasPrecision(15, 2);
        baixa.Property(x => x.Observacoes).HasColumnName("observacoes").HasMaxLength(255);
        var estorno = model.Entity<EstornoBaixa>();
        estorno.ToTable("estornos_baixas");
        estorno.HasKey(x => x.Id);
        estorno.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        estorno.Property(x => x.BaixaId).HasColumnName("baixa_id");
        estorno.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(500);
        estorno.Property(x => x.CriadoEm).HasColumnName("criado_em").HasColumnType("datetime(6)");
        estorno.HasIndex(x => x.BaixaId).IsUnique();
        estorno.HasOne<BaixaFinanceira>().WithMany().HasForeignKey(x => x.BaixaId).OnDelete(DeleteBehavior.NoAction);
        Referencia<ContaBancariaFinanceira>(model, "contas_bancarias");
        model.Entity<ContaBancariaFinanceira>().Property(x => x.Ativo).HasColumnName("ativo").HasColumnType("tinyint(1)");
        model.Entity<FormaPagamentoFinanceira>(e =>
        {
            e.ToTable("formas_pagamento"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id");
        });
        Referencia<CategoriaFinanceira>(model, "categorias");
        model.Entity<CategoriaFinanceira>().Property(x => x.Nome).HasColumnName("nome").HasMaxLength(100);
        model.Entity<CategoriaFinanceira>().Property(x => x.Tipo).HasColumnName("tipo").HasColumnType("enum('RECEITA','DESPESA')");
        Referencia<ClienteFinanceiro>(model, "clientes");
        model.Entity<ClienteFinanceiro>().Property(x => x.Nome).HasColumnName("nome").HasMaxLength(180);
        model.Entity<ClienteFinanceiro>().Property(x => x.Ativo).HasColumnName("ativo").HasColumnType("tinyint(1)");
        Referencia<FornecedorFinanceiro>(model, "fornecedores");
        model.Entity<FornecedorFinanceiro>().Property(x => x.Nome).HasColumnName("nome").HasMaxLength(180);
        model.Entity<FornecedorFinanceiro>().Property(x => x.Ativo).HasColumnName("ativo").HasColumnType("tinyint(1)");
        Referencia<CentroCustoFinanceiro>(model, "centros_custo");
        model.Entity<CentroCustoFinanceiro>().Property(x => x.Nome).HasColumnName("nome").HasMaxLength(100);
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
