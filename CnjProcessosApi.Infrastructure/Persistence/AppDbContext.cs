using CnjProcessosApi.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CnjProcessosApi.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Processo> Processos => Set<Processo>();
    public DbSet<Movimentacao> Movimentacoes => Set<Movimentacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Processo>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.NumeroCNJ).IsUnique();
            entity.Property(p => p.NumeroCNJ).IsRequired();
            entity.Property(p => p.IsSigiloso).HasDefaultValue(false);
            entity.HasMany(p => p.Movimentacoes)
                  .WithOne(m => m.Processo)
                  .HasForeignKey(m => m.ProcessoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Movimentacao>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Descricao).HasMaxLength(2000);
        });
    }
}
