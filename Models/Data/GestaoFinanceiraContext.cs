using GestaoFinanceira.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoFinanceira.Data
{
    public class GestaoFinanceiraContext : DbContext
    {
        public GestaoFinanceiraContext(
            DbContextOptions<GestaoFinanceiraContext> options
        ) : base(options)
        {
        }

        public DbSet<Empresa> Empresas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Empresa>(entity =>
            {
                entity.ToTable("empresas");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.RazaoSocial)
                    .HasColumnName("razao_social")
                    .HasMaxLength(180)
                    .IsRequired();

                entity.Property(e => e.NomeFantasia)
                    .HasColumnName("nome_fantasia")
                    .HasMaxLength(180);

                entity.Property(e => e.Cnpj)
                    .HasColumnName("cnpj")
                    .HasColumnType("char(14)");

                entity.HasIndex(e => e.Cnpj)
                    .IsUnique();

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(150);

                entity.Property(e => e.Telefone)
                    .HasColumnName("telefone")
                    .HasMaxLength(25);

                entity.Property(e => e.Ativo)
                    .HasColumnName("ativo")
                    .HasColumnType("tinyint(1)")
                    .HasDefaultValue(true);

                entity.Property(e => e.CriadoEm)
                    .HasColumnName("criado_em")
                    .HasColumnType("timestamp")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }
}