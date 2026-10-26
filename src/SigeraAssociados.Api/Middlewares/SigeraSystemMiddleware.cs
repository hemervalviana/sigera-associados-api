using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Middlewares;

/// <summary>
/// Seleciona o banco de negócio pelo cabeçalho X-Sigera-Sistema
/// e verifica se o usuário autenticado tem acesso a esse sistema.
/// Deve ser aplicado somente às rotas de negócio
/// e depois de UseAuthentication/UseAuthorization.
/// </summary>
public sealed class SigeraSystemMiddleware
{
	public const string HeaderName = "X-Sigera-Sistema";

	private readonly RequestDelegate _next;

	public SigeraSystemMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(
		HttpContext context,
		SigeraSystemContext systemContext,
		SigeraSystemResolver systemResolver,
		IEscopoAcessoService escopoAcesso)
	{
		if (!context.Request.Headers.TryGetValue(
				HeaderName, out var headerValue))
		{
			await EscreverErroAsync(
				context,
				StatusCodes.Status400BadRequest,
				$"O cabeçalho {HeaderName} é obrigatório.");

			return;
		}

		if (!int.TryParse(headerValue.ToString(), out var systemCode))
		{
			await EscreverErroAsync(
				context,
				StatusCodes.Status400BadRequest,
				$"O cabeçalho {HeaderName} deve conter um código de 1 a 4.");

			return;
		}

		string database;

		try
		{
			database = systemResolver.ResolveDatabase(systemCode);
		}
		catch (ArgumentOutOfRangeException)
		{
			await EscreverErroAsync(
				context,
				StatusCodes.Status400BadRequest,
				"Código de sistema inválido. Os valores permitidos são 1, 2, 3 e 4.");

			return;
		}

		// A autorização do endpoint já garantiu a autenticação.
		// Aqui verificamos se o usuário tem algum acesso ao sistema pedido.
		if (context.User.Identity?.IsAuthenticated == true
			&& !await escopoAcesso.PossuiAcessoAoSistemaAsync(
				systemCode,
				context.RequestAborted))
		{
			await EscreverErroAsync(
				context,
				StatusCodes.Status403Forbidden,
				"Você não tem acesso ao sistema informado.");

			return;
		}

		systemContext.SetSystem(systemCode, database);

		await _next(context);
	}

	private static Task EscreverErroAsync(
		HttpContext context,
		int statusCode,
		string mensagem)
	{
		return RespostasErro.EscreverAsync(
			context,
			statusCode,
			mensagem,
			statusCode == StatusCodes.Status400BadRequest ? "sistema_invalido" : "sistema_sem_acesso");
	}

	/// <summary>
	/// Indica se a rota exige a seleção de um sistema de negócio.
	/// </summary>
	public static bool RotaDeNegocio(PathString caminho)
	{
		return caminho.StartsWithSegments("/api/associados")
			|| caminho.StartsWithSegments("/api/cadastros")
			|| caminho.StartsWithSegments("/api/orgaos")
			|| caminho.StartsWithSegments("/api/diagnostico");
	}
}
