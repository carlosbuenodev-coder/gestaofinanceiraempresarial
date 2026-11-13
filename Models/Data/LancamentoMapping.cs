using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoFinanceira.Data;

public sealed class LancamentoMapping : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> e)
    {
        e.ToTable("lancamentos");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        e.Property(x => x.EmpresaId).HasColumnName("empresa_id");
        e.Property(x => x.Tipo).HasColumnName("tipo").HasColumnType("enum('RECEITA','DESPESA')");
        e.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(255);
        e.Property(x => x.ClienteId).HasColumnName("cliente_id");
        e.Property(x => x.FornecedorId).HasColumnName("fornecedor_id");
        e.Property(x => x.CategoriaId).HasColumnName("categoria_id");
        e.Property(x => x.CentroCustoId).HasColumnName("centro_custo_id");
        e.Property(x => x.DataEmissao).HasColumnName("data_emissao").HasColumnType("date");
        e.Property(x => x.DataVencimento).HasColumnName("data_vencimento").HasColumnType("date");
        e.Property(x => x.Valor).HasColumnName("valor").HasPrecision(15, 2);
        e.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('ABERTO','PARCIAL','QUITADO','CANCELADO')");
        e.Property(x => x.Observacoes).HasColumnName("observacoes").HasColumnType("text");
        e.Property(x => x.CriadoEm).HasColumnName("criado_em").HasColumnType("timestamp").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.HasIndex(x => new { x.EmpresaId, x.DataVencimento, x.Status }).HasDatabaseName("idx_lanc_empresa_venc");
    }
}
