using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SigeraAssociados.Infrastructure.Saude;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Saude;

/// <summary>
/// Health checks da API.
///
/// - GET /health: a API está no ar (não consulta bancos). Para o
///   monitoramento saber se o processo responde.
/// - GET /health/pronto: confere a conexão com o SigeraIdentity e com as
///   4 bases de negócio. 200 se todas respondem; 503 se alguma falhar.
///
/// As duas rotas são públicas e não mostram detalhes de erro.
/// </summary>
public static class VerificacoesSaude
{
	public const string RotaVivo = "/health";
	public const string RotaPronto = "/health/pronto";

	private const string TagPronto = "pronto";
	private static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(5);

	public static IServiceCollection AddVerificacoesSaude(this IServiceCollection services)
	{
		var checks = services.AddHealthChecks()
			.AddCheck<VerificacaoBancoIdentidade>(
				"SigeraIdentity",
				tags: [TagPronto],
				timeout: TempoLimite);

		foreach (var sistema in Enum.GetValues<SigeraSystem>())
		{
			checks.AddTypeActivatedCheck<VerificacaoBancoSistema>(
				sistema.ToString(),
				failureStatus: null,
				tags: [TagPronto],
				timeout: TempoLimite,
				args: new object[] { (int)sistema });
		}

		return services;
	}

	public static IEndpointRouteBuilder MapVerificacoesSaude(this IEndpointRouteBuilder app)
	{
		app.MapHealthChecks(RotaVivo, new HealthCheckOptions
		{
			Predicate = _ => false,
			ResponseWriter = EscreverAsync
		});

		app.MapHealthChecks(RotaPronto, new HealthCheckOptions
		{
			Predicate = registro => registro.Tags.Contains(TagPronto),
			ResponseWriter = EscreverAsync
		});

		return app;
	}

	private static Task EscreverAsync(HttpContext contexto, HealthReport relatorio)
	{
		contexto.Response.Headers.CacheControl = "no-store";

		return contexto.Response.WriteAsJsonAsync(new
		{
			status = relatorio.Status.ToString(),
			duracaoMs = (int)relatorio.TotalDuration.TotalMilliseconds,
			verificacoes = relatorio.Entries.Select(item => new
			{
				nome = item.Key,
				status = item.Value.Status.ToString(),
				duracaoMs = (int)item.Value.Duration.TotalMilliseconds,
				descricao = item.Value.Description
			})
		});
	}
}
