using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace SigeraAssociados.Api.Erros;

/// <summary>
/// Formato único das respostas de erro da API: ProblemDetails (RFC 9457),
/// com content-type application/problem+json.
///
/// Campos: type, title (em português), status, detail (mensagem),
/// instance (rota), codigo (identificador estável) e traceId
/// (para localizar a falha no log).
/// </summary>
public static class RespostasErro
{
	private static readonly IReadOnlyDictionary<int, (string Titulo, string Codigo)> PorStatus =
		new Dictionary<int, (string, string)>
		{
			[StatusCodes.Status400BadRequest] = ("Requisição inválida", "requisicao_invalida"),
			[StatusCodes.Status401Unauthorized] = ("Não autenticado", "nao_autenticado"),
			[StatusCodes.Status403Forbidden] = ("Acesso negado", "acesso_negado"),
			[StatusCodes.Status404NotFound] = ("Não encontrado", "nao_encontrado"),
			[StatusCodes.Status405MethodNotAllowed] = ("Método não permitido", "metodo_nao_permitido"),
			[StatusCodes.Status409Conflict] = ("Conflito", "conflito"),
			[StatusCodes.Status415UnsupportedMediaType] = ("Tipo de conteúdo não suportado", "tipo_nao_suportado"),
			[StatusCodes.Status423Locked] = ("Conta bloqueada", "conta_bloqueada"),
			[StatusCodes.Status429TooManyRequests] = ("Muitas requisições", "limite_excedido"),
			[StatusCodes.Status500InternalServerError] = ("Erro interno", "erro_interno"),
			[StatusCodes.Status503ServiceUnavailable] = ("Serviço indisponível", "servico_indisponivel")
		};

	/// <summary>
	/// Título padrão em português para o status.
	/// </summary>
	public static string Titulo(int status) =>
		PorStatus.TryGetValue(status, out var item)
			? item.Titulo
			: ReasonPhrases.GetReasonPhrase(status);

	/// <summary>
	/// Código padrão para o status.
	/// </summary>
	public static string Codigo(int status) =>
		PorStatus.TryGetValue(status, out var item) ? item.Codigo : "erro";

	/// <summary>
	/// Completa qualquer ProblemDetails da API (controllers, validação
	/// automática, exceções, status code pages): título em português,
	/// instance, codigo e traceId.
	/// </summary>
	public static void Completar(ProblemDetails problema, HttpContext contexto)
	{
		var status = problema.Status ?? contexto.Response.StatusCode;
		problema.Status = status;

		// Substitui os títulos padrão em inglês ("Bad Request", "One or more validation errors occurred.").
		if (string.IsNullOrWhiteSpace(problema.Title)
			|| problema.Title == ReasonPhrases.GetReasonPhrase(status)
			|| problema.Title.StartsWith("One or more validation errors", StringComparison.Ordinal))
		{
			problema.Title = problema is ValidationProblemDetails
				? "Um ou mais campos são inválidos"
				: Titulo(status);
		}

		problema.Instance ??= contexto.Request.Path;

		if (!problema.Extensions.ContainsKey("codigo"))
		{
			problema.Extensions["codigo"] = problema is ValidationProblemDetails
				? "validacao"
				: Codigo(status);
		}

		if (!problema.Extensions.ContainsKey("traceId"))
		{
			problema.Extensions["traceId"] =
				Activity.Current?.Id ?? contexto.TraceIdentifier;
		}
	}

	/// <summary>
	/// Resposta de erro para controllers.
	/// </summary>
	public static ObjectResult Erro(
		this ControllerBase controller,
		int status,
		string detalhe,
		string? codigo = null,
		IReadOnlyDictionary<string, object?>? extras = null)
	{
		var problema = controller.ProblemDetailsFactory.CreateProblemDetails(
			controller.HttpContext,
			statusCode: status,
			title: Titulo(status),
			detail: detalhe);

		problema.Extensions["codigo"] = codigo ?? Codigo(status);

		if (extras is not null)
		{
			foreach (var (chave, valor) in extras)
			{
				problema.Extensions[chave] = valor;
			}
		}

		Completar(problema, controller.HttpContext);

		var resultado = new ObjectResult(problema) { StatusCode = status };
		resultado.ContentTypes.Add("application/problem+json");

		return resultado;
	}

	/// <summary>
	/// Escreve uma resposta de erro fora dos controllers
	/// (middleware, limitador de requisições, tratador de exceções).
	/// </summary>
	public static async Task EscreverAsync(
		HttpContext contexto,
		int status,
		string detalhe,
		string? codigo = null,
		IReadOnlyDictionary<string, object?>? extras = null,
		Exception? excecao = null)
	{
		var problema = new ProblemDetails
		{
			Status = status,
			Title = Titulo(status),
			Detail = detalhe
		};

		problema.Extensions["codigo"] = codigo ?? Codigo(status);

		if (extras is not null)
		{
			foreach (var (chave, valor) in extras)
			{
				problema.Extensions[chave] = valor;
			}
		}

		contexto.Response.StatusCode = status;

		var servico = contexto.RequestServices.GetRequiredService<IProblemDetailsService>();

		var escrito = await servico.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = contexto,
			ProblemDetails = problema,
			Exception = excecao
		});

		if (!escrito)
		{
			Completar(problema, contexto);
			await contexto.Response.WriteAsJsonAsync(
				problema,
				options: null,
				contentType: "application/problem+json");
		}
	}
}
