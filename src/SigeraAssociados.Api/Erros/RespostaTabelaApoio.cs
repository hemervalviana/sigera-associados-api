using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Api.Erros;

/// <summary>
/// Converte ResultadoTabelaApoio em resposta HTTP:
/// sucesso 200 (ou 201), não encontrado 404, inválido 400, conflito 409.
/// </summary>
public static class RespostaTabelaApoio
{
	public static IActionResult Converter<T>(
		this ControllerBase controller,
		ResultadoTabelaApoio<T> resultado,
		bool criado = false)
	{
		if (resultado.Status == StatusTabelaApoio.Sucesso)
		{
			var corpo = new { mensagem = resultado.Mensagem, item = resultado.Item };

			return criado
				? controller.StatusCode(StatusCodes.Status201Created, corpo)
				: controller.Ok(corpo);
		}

		var status = resultado.Status switch
		{
			StatusTabelaApoio.NaoEncontrado => StatusCodes.Status404NotFound,
			StatusTabelaApoio.Invalido => StatusCodes.Status400BadRequest,
			StatusTabelaApoio.Conflito => StatusCodes.Status409Conflict,
			StatusTabelaApoio.Proibido => StatusCodes.Status403Forbidden,
			_ => StatusCodes.Status500InternalServerError
		};

		return controller.Erro(status, resultado.Mensagem ?? RespostasErro.Titulo(status));
	}
}
