
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Infrastructure.Persistence.Entities;


namespace SigeraAssociados.Infrastructure.Persistence.Context;

/// <summary>
/// Contexto de acesso aos bancos legados do SIGERA.
/// O banco utilizado é definido dinamicamente pela fábrica
/// conforme o sistema selecionado na requisição.
/// </summary>
public sealed class SigeraDbContext : DbContext
{
	public SigeraDbContext(
		DbContextOptions<SigeraDbContext> options)
		: base(options)
	{
	}

	public DbSet<T009EMPS> Empresas => Set<T009EMPS>();
	public DbSet<T000PESS> Pessoas => Set<T000PESS>();
	public DbSet<T000CLIE> Associados => Set<T000CLIE>();
	public DbSet<T000SCLI> SituacoesAssociados => Set<T000SCLI>();
	public DbSet<T009CTRT> Contratos => Set<T009CTRT>();
	public DbSet<T000PFIS> PessoasFisicas => Set<T000PFIS>();
	public DbSet<T000PJUR> PessoasJuridicas => Set<T000PJUR>();

	/// <summary>
	/// Aplica automaticamente as configurações de entidades
	/// existentes neste assembly.
	///
	/// Cada tabela terá seu próprio arquivo de configuração
	/// dentro de Persistence/Configurations.
	/// </summary>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly(
			typeof(SigeraDbContext).Assembly);
	}
}
