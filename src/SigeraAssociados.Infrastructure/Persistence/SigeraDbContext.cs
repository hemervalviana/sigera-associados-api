
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
	public DbSet<T000PARA> Filiais => Set<T000PARA>();
	public DbSet<T009CRRT> Corretores => Set<T009CRRT>();
	public DbSet<T000MUNI> Municipios => Set<T000MUNI>();
	public DbSet<T000TRTA> Tratamentos => Set<T000TRTA>();
	public DbSet<T000ORGA> Orgaos => Set<T000ORGA>();
	public DbSet<T000LOTA> Lotacoes => Set<T000LOTA>();
	public DbSet<T000CARG> Cargos => Set<T000CARG>();
	public DbSet<T009GRUP> Grupos => Set<T009GRUP>();
	public DbSet<T009SBOR> SubOrgaos => Set<T009SBOR>();
	public DbSet<T000SPOG> SituacoesVinculo => Set<T000SPOG>();
	public DbSet<T000RPOR> VinculosOrgao => Set<T000RPOR>();
	public DbSet<T000TEND> TiposEndereco => Set<T000TEND>();
	public DbSet<T000ENDE> Enderecos => Set<T000ENDE>();
	public DbSet<T000BANC> Bancos => Set<T000BANC>();
	public DbSet<T000CCOR> ContasBancarias => Set<T000CCOR>();
	public DbSet<T000TTEL> TiposTelefone => Set<T000TTEL>();
	public DbSet<T000TELE> Telefones => Set<T000TELE>();
	public DbSet<T009PIX> ChavesPix => Set<T009PIX>();
	public DbSet<T000REFE> Referencias => Set<T000REFE>();
	public DbSet<T009FRPG> FormasPagamento => Set<T009FRPG>();
	public DbSet<T009PRDT> Produtos => Set<T009PRDT>();
	public DbSet<T009CONV> Convenios => Set<T009CONV>();
	public DbSet<T009EXTRA> Creditos => Set<T009EXTRA>();
	public DbSet<T009AGENT> Agentes => Set<T009AGENT>();
	public DbSet<T009GVED> GerentesVendas => Set<T009GVED>();
	public DbSet<T009PAGA> Parcelas => Set<T009PAGA>();
	public DbSet<T009SCTR> SituacoesContrato => Set<T009SCTR>();
	public DbSet<T009CARE> Carencias => Set<T009CARE>();
	public DbSet<T009OREC> OrigensRecurso => Set<T009OREC>();
	public DbSet<T009PROD> Produtores => Set<T009PROD>();
	public DbSet<T009INDI> Indices => Set<T009INDI>();
	public DbSet<T009PARM> Parametros => Set<T009PARM>();
	public DbSet<T009CAT> Categorias => Set<T009CAT>();

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

		// As tabelas do legado podem ter triggers que a API não conhece.
		// Marcar cada tabela faz o Entity Framework gravar sem OUTPUT,
		// que o SQL Server recusa em tabela com trigger. Não muda nada
		// nas leituras e o nome não precisa existir no banco.
		foreach (var tipo in modelBuilder.Model.GetEntityTypes().ToList())
		{
			modelBuilder.Entity(tipo.ClrType)
				.ToTable(tabela => tabela.HasTrigger($"TR_LEGADO_{tipo.ClrType.Name}"));
		}
	}
}
