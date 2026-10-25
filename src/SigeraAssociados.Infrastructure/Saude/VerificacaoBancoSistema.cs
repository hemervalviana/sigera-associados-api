using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SigeraAssociados.Infrastructure.Persistence.Factories;

namespace SigeraAssociados.Infrastructure.Saude;

/// <summary>
/// Verifica a conexão com o banco de negócio de um sistema
/// (1 PROASP, 2 SONASP, 3 ABSP, 4 GSP).
/// A resposta não expõe detalhes do erro; eles ficam no log.
/// </summary>
public sealed class VerificacaoBancoSistema : IHealthCheck
{
	private readonly int _sistema;
	private readonly SigeraDbContextFactory _factory;
	private readonly ILogger<VerificacaoBancoSistema> _logger;

	public VerificacaoBancoSistema(
		int sistema,
		SigeraDbContextFactory factory,
		ILogger<VerificacaoBancoSistema> logger)
	{
		_sistema = sistema;
		_factory = factory;
		_logger = logger;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var db = _factory.Create(_sistema);

			return await db.Database.CanConnectAsync(cancellationToken)
				? HealthCheckResult.Healthy("Conectado.")
				: HealthCheckResult.Unhealthy("Sem conexão.");
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Health check: falha ao conectar no banco do sistema {Sistema}.", _sistema);
			return HealthCheckResult.Unhealthy("Sem conexão.");
		}
	}
}
