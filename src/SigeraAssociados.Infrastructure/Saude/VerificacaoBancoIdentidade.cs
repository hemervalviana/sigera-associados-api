using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SigeraAssociados.Infrastructure.Persistence.Identity;

namespace SigeraAssociados.Infrastructure.Saude;

/// <summary>
/// Verifica a conexão com o banco de identidade (SigeraIdentity).
/// A resposta não expõe detalhes do erro; eles ficam no log.
/// </summary>
public sealed class VerificacaoBancoIdentidade : IHealthCheck
{
	private readonly SigeraIdentityDbContext _identityDb;
	private readonly ILogger<VerificacaoBancoIdentidade> _logger;

	public VerificacaoBancoIdentidade(
		SigeraIdentityDbContext identityDb,
		ILogger<VerificacaoBancoIdentidade> logger)
	{
		_identityDb = identityDb;
		_logger = logger;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			return await _identityDb.Database.CanConnectAsync(cancellationToken)
				? HealthCheckResult.Healthy("Conectado.")
				: HealthCheckResult.Unhealthy("Sem conexão.");
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Health check: falha ao conectar no banco de identidade.");
			return HealthCheckResult.Unhealthy("Sem conexão.");
		}
	}
}
