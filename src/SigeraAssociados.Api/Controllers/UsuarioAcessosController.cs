using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.DTOs.Acessos;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Gerencia os escopos de acesso (sistema, empresa, filial e carteira
/// do corretor) de cada usuário. Restrito a administradores.
/// </summary>
[ApiController]
[Route("api/administracao/usuarios/{usuarioId}/acessos")]
[Authorize(Roles = Grupos.PerfisAdministracao)]
public sealed class UsuarioAcessosController : ControllerBase
{
	private readonly IGestaoAcessosService _gestaoAcessos;
	private readonly ILogger<UsuarioAcessosController> _logger;

	public UsuarioAcessosController(
		IGestaoAcessosService gestaoAcessos,
		ILogger<UsuarioAcessosController> logger)
	{
		_gestaoAcessos = gestaoAcessos;
		_logger = logger;
	}

	/// <summary>
	/// Lista os acessos cadastrados para o usuário.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(
		string usuarioId,
		CancellationToken cancellationToken)
	{
		var acessos = await _gestaoAcessos.ListarAsync(
			usuarioId,
			cancellationToken);

		if (acessos is null)
		{
			return NotFound(new { mensagem = "Usuário não encontrado." });
		}

		return Ok(new
		{
			quantidade = acessos.Count,
			acessos
		});
	}

	/// <summary>
	/// Concede um novo escopo de acesso ao usuário.
	/// </summary>
	[HttpPost]
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Conceder(
		string usuarioId,
		[FromBody] ConcederAcessoRequest request,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoAcessos.ConcederAsync(
			usuarioId,
			new NovoAcessoDto
			{
				Sistema = request.Sistema,
				CdEmp = request.CdEmp,
				CdFil = request.CdFil,
				SqPesCorretor = request.SqPesCorretor
			},
			cancellationToken);

		if (resultado.Status == StatusGestaoAcesso.Sucesso)
		{
			_logger.LogInformation(
				"Acesso {AcessoId} concedido ao usuário {UsuarioId} por {Administrador}.",
				resultado.Acesso!.Id,
				usuarioId,
				User.Identity?.Name ?? "(desconhecido)");

			return StatusCode(
				StatusCodes.Status201Created,
				new
				{
					mensagem = "Acesso concedido.",
					acesso = resultado.Acesso
				});
		}

		return ParaResposta(resultado);
	}

	/// <summary>
	/// Revoga um escopo de acesso do usuário.
	/// O efeito é imediato nesta instância da API.
	/// </summary>
	[HttpDelete("{acessoId:int}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Revogar(
		string usuarioId,
		int acessoId,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoAcessos.RevogarAsync(
			usuarioId,
			acessoId,
			cancellationToken);

		if (resultado.Status == StatusGestaoAcesso.Sucesso)
		{
			_logger.LogInformation(
				"Acesso {AcessoId} revogado do usuário {UsuarioId} por {Administrador}.",
				acessoId,
				usuarioId,
				User.Identity?.Name ?? "(desconhecido)");

			return Ok(new
			{
				mensagem = "Acesso revogado.",
				acesso = resultado.Acesso
			});
		}

		return ParaResposta(resultado);
	}

	private IActionResult ParaResposta(ResultadoGestaoAcesso resultado)
	{
		var corpo = new { mensagem = resultado.Mensagem };

		return resultado.Status switch
		{
			StatusGestaoAcesso.UsuarioNaoEncontrado => NotFound(corpo),
			StatusGestaoAcesso.AcessoNaoEncontrado => NotFound(corpo),
			StatusGestaoAcesso.Duplicado => Conflict(corpo),
			_ => BadRequest(corpo)
		};
	}
}
