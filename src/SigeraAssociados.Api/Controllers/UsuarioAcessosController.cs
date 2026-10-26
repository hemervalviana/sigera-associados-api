using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Api.DTOs.Acessos;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Gerencia os escopos de acesso (sistema, filial e carteira do corretor)
/// de cada usuário. Restrito a administradores.
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
			return this.Erro(
				StatusCodes.Status404NotFound,
				"Usuário não encontrado.");
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
	/// <remarks>
	/// Cada sistema é uma base com uma única empresa, por isso a empresa
	/// não é informada.
	///
	/// `sistema`: 0 Todos, 1 PROASP, 2 SONASP, 3 ABSP, 4 GSP.
	/// Todos libera os sistemas inteiros (grava uma linha por sistema que o
	/// usuário ainda não tem) e não aceita filial nem corretor.
	///
	/// Exemplos:
	///
	///     { "sistema": 0 }                          todos os sistemas
	///     { "sistema": 2 }                          SONASP inteiro
	///     { "sistema": 2, "cdFil": 1 }              filial 1 do SONASP
	///     { "sistema": 2, "sqPesCorretor": 12345 }  carteira do corretor no SONASP
	/// </remarks>
	/// <response code="201">Concedido. `acessos` traz as linhas gravadas (várias com sistema 0).</response>
	/// <response code="400">Dados inválidos, filial ou corretor inexistente, ou Todos com filial/corretor.</response>
	/// <response code="404">Usuário não encontrado.</response>
	/// <response code="409">O acesso já existe (com Todos: o usuário já tem todos os sistemas).</response>
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
				CdFil = request.CdFil,
				SqPesCorretor = request.SqPesCorretor
			},
			cancellationToken);

		if (resultado.Status == StatusGestaoAcesso.Sucesso)
		{
			IReadOnlyList<UsuarioAcessoDto> acessos = resultado.Acessos
				?? (resultado.Acesso is null
					? Array.Empty<UsuarioAcessoDto>()
					: new[] { resultado.Acesso });

			_logger.LogInformation(
				"Acessos {AcessoIds} concedidos ao usuário {UsuarioId} por {Administrador}.",
				string.Join(", ", acessos.Select(a => a.Id)),
				usuarioId,
				User.Identity?.Name ?? "(desconhecido)");

			return StatusCode(
				StatusCodes.Status201Created,
				new
				{
					mensagem = resultado.Mensagem ?? "Acesso concedido.",
					acesso = resultado.Acesso,
					quantidade = acessos.Count,
					acessos
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
		var status = resultado.Status switch
		{
			StatusGestaoAcesso.UsuarioNaoEncontrado => StatusCodes.Status404NotFound,
			StatusGestaoAcesso.AcessoNaoEncontrado => StatusCodes.Status404NotFound,
			StatusGestaoAcesso.Duplicado => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status400BadRequest
		};

		return this.Erro(
			status,
			resultado.Mensagem ?? RespostasErro.Titulo(status));
	}
}
