
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SigeraAssociados.Infrastructure.Persistence.Configuration;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Persistence.Factories;

/// <summary>
/// Cria contextos do Entity Framework Core para o banco
/// correspondente ao sistema SIGERA selecionado.
/// </summary>
public sealed class SigeraDbContextFactory
{
	private readonly SqlServerOptions _options;
	private readonly SigeraSystemResolver _systemResolver;

	public SigeraDbContextFactory(
		IOptions<SqlServerOptions> options,
		SigeraSystemResolver systemResolver)
	{
		_options = options.Value;
		_systemResolver = systemResolver;
	}

	/// <summary>
	/// Cria um novo contexto para o banco do sistema informado.
	/// </summary>
	public SigeraDbContext Create(int systemCode)
	{
		var database = _systemResolver.ResolveDatabase(systemCode);

		var connectionStringBuilder = new SqlConnectionStringBuilder
		{
			DataSource = $"{_options.Server},{_options.Port}",
			InitialCatalog = database,
			UserID = _options.User,
			Password = _options.Password,
			Encrypt = _options.Encrypt,
			TrustServerCertificate = _options.TrustServerCertificate
		};

		var dbContextOptions =
			new DbContextOptionsBuilder<SigeraDbContext>()
				.UseSqlServer(
					connectionStringBuilder.ConnectionString,
					sql => sql.UseCompatibilityLevel(_options.CompatibilityLevel))
				.Options;

		return new SigeraDbContext(dbContextOptions);
	}
}
