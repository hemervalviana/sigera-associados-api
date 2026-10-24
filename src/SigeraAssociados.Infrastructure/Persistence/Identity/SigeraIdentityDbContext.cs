using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Identity;

/// <summary>
/// Contexto exclusivo da identidade centralizada da API.
/// Não contém entidades dos quatro bancos de negócio.
/// </summary>
public sealed class SigeraIdentityDbContext
    : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    public SigeraIdentityDbContext(
        DbContextOptions<SigeraIdentityDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Escopos de acesso dos usuários aos sistemas, empresas e filiais.
    /// </summary>
    public DbSet<UsuarioAcesso> UsuarioAcessos => Set<UsuarioAcesso>();

    /// <summary>
    /// Tokens de renovação (refresh tokens), gravados como hash.
    /// </summary>
    public DbSet<TokenRenovacao> TokensRenovacao => Set<TokenRenovacao>();

    /// <summary>
    /// Configura o modelo do ASP.NET Core Identity.
    /// As tabelas serão criadas somente após a geração e aplicação
    /// explícita das migrações do banco SigeraIdentity.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Define nomes explícitos para facilitar a administração do banco.
        builder.Entity<IdentityUser>().ToTable("Usuarios");
        builder.Entity<IdentityRole>().ToTable("Funcoes");
        builder.Entity<IdentityUserRole<string>>().ToTable("UsuarioFuncoes");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UsuarioReivindicacoes");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UsuarioLogins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("FuncaoReivindicacoes");
        builder.Entity<IdentityUserToken<string>>().ToTable("UsuarioTokens");

        builder.Entity<UsuarioAcesso>(entidade =>
        {
            entidade.ToTable("UsuarioAcessos", tabela =>
            {
                tabela.HasCheckConstraint(
                    "CK_UsuarioAcessos_Sistema",
                    "[Sistema] BETWEEN 1 AND 4");

                // Filial exige empresa.
                tabela.HasCheckConstraint(
                    "CK_UsuarioAcessos_FilialExigeEmpresa",
                    "[CdFil] IS NULL OR [CdEmp] IS NOT NULL");

                // Carteira de corretor exige empresa,
                // pois SQ_PES é único apenas dentro da empresa.
                tabela.HasCheckConstraint(
                    "CK_UsuarioAcessos_CorretorExigeEmpresa",
                    "[SqPesCorretor] IS NULL OR [CdEmp] IS NOT NULL");
            });

            entidade.HasKey(a => a.Id);

            entidade.Property(a => a.UsuarioId)
                .HasMaxLength(450)
                .IsRequired();

            entidade.Property(a => a.CdEmp).HasPrecision(2, 0);
            entidade.Property(a => a.CdFil).HasPrecision(2, 0);
            entidade.Property(a => a.SqPesCorretor).HasPrecision(7, 0);

            entidade.Property(a => a.CriadoEmUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entidade.HasIndex(a => new
                {
                    a.UsuarioId,
                    a.Sistema,
                    a.CdEmp,
                    a.CdFil,
                    a.SqPesCorretor
                })
                .IsUnique()
                .HasDatabaseName("UX_UsuarioAcessos_Escopo");

            entidade.HasOne(a => a.Usuario)
                .WithMany()
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TokenRenovacao>(entidade =>
        {
            entidade.ToTable("TokensRenovacao");

            entidade.HasKey(t => t.Id);

            entidade.Property(t => t.UsuarioId)
                .HasMaxLength(450)
                .IsRequired();

            entidade.Property(t => t.TokenHash)
                .HasMaxLength(64)
                .IsUnicode(false)
                .IsRequired();

            entidade.Property(t => t.SubstituidoPorHash)
                .HasMaxLength(64)
                .IsUnicode(false);

            entidade.Property(t => t.MotivoRevogacao)
                .HasMaxLength(30)
                .IsUnicode(false);

            entidade.HasIndex(t => t.TokenHash)
                .IsUnique()
                .HasDatabaseName("UX_TokensRenovacao_TokenHash");

            entidade.HasIndex(t => new { t.UsuarioId, t.RevogadoEmUtc })
                .HasDatabaseName("IX_TokensRenovacao_Usuario_Ativos");

            entidade.HasOne(t => t.Usuario)
                .WithMany()
                .HasForeignKey(t => t.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
