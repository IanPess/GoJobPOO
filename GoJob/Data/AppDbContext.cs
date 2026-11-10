using GoJob.Domain;
using Microsoft.EntityFrameworkCore;

namespace GoJob.Data
{
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

            // 1. Mapeamento de Herança (TPH - Table Per Hierarchy)[cite: 1, 2]
            modelBuilder.Entity<Usuario>()
                .HasDiscriminator<string>("TipoUsuario")
                .HasValue<Cliente>("Cliente")
                .HasValue<Profissional>("Profissional")
                .HasValue<Administrador>("Administrador"); // Padronizado com o construtor da classe

            // 2. Mapeamento da navegação encapsulada de Profissional (private set)
            modelBuilder.Entity<Profissional>()
                .HasMany(p => p.Categorias)
                .WithMany(); // Se for N:N simples sem entidade de junção explícita

            // 3. Relacionamento 1 para 1: Usuario <-> Endereco[cite: 2]
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Endereco)
                .WithOne(e => e.Usuario)
                .HasForeignKey<Endereco>(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade); //[cite: 2]

            // 4. Relacionamento 1 para N: Cliente <-> Avaliacao[cite: 2]
            modelBuilder.Entity<Avaliacao>()
                .HasOne(a => a.Cliente)
                .WithMany(c => c.AvaliacoesRealizadas)
                .HasForeignKey(a => a.ClienteId)
                .OnDelete(DeleteBehavior.Restrict); //[cite: 2]

            // 5. Relacionamento 1 para N: Profissional <-> Avaliacao[cite: 2]
            modelBuilder.Entity<Avaliacao>()
                .HasOne(a => a.Profissional)
                .WithMany(p => p.AvaliacoesRecebidas)
                .HasForeignKey(a => a.ProfissionalId)
                .OnDelete(DeleteBehavior.Restrict); //[cite: 2]
        }
    }
}