using Microsoft.AspNetCore.Diagnostics;
using SigeraAssociados.Application.Erros;

namespace SigeraAssociados.Api.Erros;

/// <summary>
/// Tratador global de exceções.
///
/// - DadosInvalidosException: 400 com a mensagem para o consumidor.
/// - Requisição malformada (BadHttpRequestException): status da exceção.
/// - Cancelamento pelo cliente: encerra sem corpo.
/// - Qualquer outra exceção: registra no log com o traceId e responde 500
///   sem expor detalhes internos (em Development, inclui tipo e mensagem).
/// </summary>
public sealed class TratadorExcecoes : IExceptionHandler
{
	private readonly ILogger<TratadorExcecoes> _logger;
	private readonly IHostEnvironment _ambiente;

	public TratadorExcecoes(
		ILogger<TratadorExcecoes> logger,
		IHostEnvironment ambiente)
	{
		_logger = logger;
		_ambiente = ambiente;
	}

	public async ValueTask<bool> TryHandleAsync(
		HttpContext contexto,
		Exception excecao,
		CancellationToken cancellationToken)
	{
		switch (excecao)
		{
			case OperationCanceledException when contexto.RequestAborted.IsCancellationRequested:
				// O cliente desistiu da requisição; não há a quem responder.
				_logger.LogInformation(
					"Requisição cancelada pelo cliente: {Metodo} {Caminho}.",
					contexto.Request.Method,
					contexto.Request.Path.Value);
				return true;

			case DadosInvalidosException invalida:
				await RespostasErro.EscreverAsync(
					contexto,
					StatusCodes.Status400BadRequest,
					invalida.Message,
					"parametro_invalido",
					invalida.Detalhes);
				return true;

			case BadHttpRequestException malformada:
				await RespostasErro.EscreverAsync(
					contexto,
					malformada.StatusCode,
					"A requisição está malformada.",
					excecao: excecao);
				return true;
		}

		var traceId = System.Diagnostics.Activity.Current?.Id ?? contexto.TraceIdentifier;

		_logger.LogError(
			excecao,
			"Falha não tratada em {Metodo} {Caminho}. TraceId: {TraceId}.",
			contexto.Request.Method,
			contexto.Request.Path.Value,
			traceId);

		var extras = _ambiente.IsDevelopment()
			? new Dictionary<string, object?>
			{
				["excecao"] = excecao.GetType().FullName,
				["mensagemExcecao"] = excecao.Message
			}
			: null;

		await RespostasErro.EscreverAsync(
			contexto,
			StatusCodes.Status500InternalServerError,
			"Ocorreu um erro inesperado. Informe o traceId ao suporte para localizar a falha no log.",
			"erro_interno",
			extras,
			excecao);

		return true;
	}
}
