using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SigeraAssociados.Api.Seguranca;

/// <summary>
/// Limites de requisições da API (rate limiting nativo do ASP.NET Core).
///
/// - Geral: 300 requisições por minuto por usuário (ou por IP, se anônimo).
/// - Autenticação (login, confirmação e recuperação de senha):
///   10 requisições por minuto por IP.
/// - Envio de e-mails (criar usuário e reenviar confirmação):
///   5 envios a cada 10 minutos por usuário.
///
/// Acima do limite a resposta é 429, com o cabeçalho Retry-After.
/// O IP do cliente vem do X-Forwarded-For quando a requisição passa
/// pelo cloudflared (UseForwardedHeaders no Program.cs).
/// </summary>
public static class LimiteRequisicoes
{
	/// <summary>
	/// Política dos endpoints de autenticação. Uso: [EnableRateLimiting(...)].
	/// </summary>
	public const string Autenticacao = "autenticacao";

	/// <summary>
	/// Política dos endpoints que enviam e-mail.
	/// </summary>
	public const string EnvioEmail = "envio-email";

	public static IServiceCollection AddLimiteRequisicoes(
		this IServiceCollection services)
	{
		services.AddRateLimiter(options =>
		{
			options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

			options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
				contexto => RateLimitPartition.GetFixedWindowLimiter(
					ChaveUsuarioOuIp(contexto),
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 300,
						Window = TimeSpan.FromMinutes(1),
						QueueLimit = 0
					}));

			// Janela deslizante: evita rajadas na virada do minuto.
			options.AddPolicy(Autenticacao, contexto =>
				RateLimitPartition.GetSlidingWindowLimiter(
					"ip:" + Ip(contexto),
					_ => new SlidingWindowRateLimiterOptions
					{
						PermitLimit = 10,
						Window = TimeSpan.FromMinutes(1),
						SegmentsPerWindow = 6,
						QueueLimit = 0
					}));

			options.AddPolicy(EnvioEmail, contexto =>
				RateLimitPartition.GetFixedWindowLimiter(
					ChaveUsuarioOuIp(contexto),
					_ => new FixedWindowRateLimiterOptions
					{
						PermitLimit = 5,
						Window = TimeSpan.FromMinutes(10),
						QueueLimit = 0
					}));

			options.OnRejected = async (contexto, cancellationToken) =>
			{
				var resposta = contexto.HttpContext.Response;
				int? segundos = null;

				if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
				{
					segundos = (int)Math.Ceiling(espera.TotalSeconds);
					resposta.Headers.RetryAfter =
						segundos.Value.ToString(CultureInfo.InvariantCulture);
				}

				contexto.HttpContext.RequestServices
					.GetRequiredService<ILoggerFactory>()
					.CreateLogger("LimiteRequisicoes")
					.LogWarning(
						"Limite de requisições excedido em {Metodo} {Caminho}.",
						contexto.HttpContext.Request.Method,
						contexto.HttpContext.Request.Path.Value);

				await resposta.WriteAsJsonAsync(
					new
					{
						mensagem = "Muitas requisições em pouco tempo. Aguarde e tente novamente.",
						tenteNovamenteEmSegundos = segundos
					},
					cancellationToken);
			};
		});

		return services;
	}

	private static string Ip(HttpContext contexto)
	{
		return contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
	}

	/// <summary>
	/// Usuário autenticado: limite por usuário. Anônimo: limite por IP.
	/// </summary>
	private static string ChaveUsuarioOuIp(HttpContext contexto)
	{
		var usuarioId = contexto.User.Identity?.IsAuthenticated == true
			? contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)
			: null;

		return string.IsNullOrWhiteSpace(usuarioId)
			? "ip:" + Ip(contexto)
			: "usuario:" + usuarioId;
	}
}
