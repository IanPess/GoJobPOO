using GoJob.Domain;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace GoJob.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Tabelas do Banco de Dados
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Profissional> Profissionais { get; set; }
    public DbSet<Administrador> Administradores { get; set; }
    public DbSet<Endereco> Enderecos { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Avaliacao> Avaliacoes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Mapeamento de Herança (TTP - Table Per Hierarchy)
        modelBuilder.Entity<Usuario>()
            .HasDiscriminator<string>("TipoUsuario")
            .HasValue<Cliente>("Cliente")
            .HasValue<Profissional>("Profissional")
            .HasValue<Administrador>("Administrador");

        // Relacionamento 1 para 1: Usuario <-> Endereco
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Endereco)
            .WithOne(e => e.Usuario)
            .HasForeignKey<Endereco>(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relacionamento 1 para N: Cliente <-> Avaliacao
        modelBuilder.Entity<Avaliacao>()
            .HasOne(a => a.Cliente)
            .WithMany(c => c.AvaliacoesRealizadas)
            .HasForeignKey(a => a.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relacionamento 1 para N: Profissional <-> Avaliacao
        modelBuilder.Entity<Avaliacao>()
            .HasOne(a => a.Profissional)
            .WithMany(p => p.AvaliacoesRecebidas)
            .HasForeignKey(a => a.ProfissionalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}