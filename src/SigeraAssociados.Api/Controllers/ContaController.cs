using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Dados da conta do usuário autenticado.
/// Não exige o cabeçalho X-Sigera-Sistema.
/// </summary>
[ApiController]
[Route("api/conta")]
[Authorize]
public sealed class ContaController : ControllerBase
{
	private readonly IUsuarioAtual _usuarioAtual;
	private readonly IGestaoUsuariosService _gestaoUsuarios;
	private readonly IGestaoAcessosService _gestaoAcessos;

	public ContaController(
		IUsuarioAtual usuarioAtual,
		IGestaoUsuariosService gestaoUsuarios,
		IGestaoAcessosService gestaoAcessos)
	{
		_usuarioAtual = usuarioAtual;
		_gestaoUsuarios = gestaoUsuarios;
		_gestaoAcessos = gestaoAcessos;
	}

	/// <summary>
	/// Retorna o usuário logado, seus grupos e os sistemas, empresas,
	/// filiais e carteiras que ele pode consultar.
	/// Útil para o frontend montar os filtros disponíveis.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Obter(CancellationToken cancellationToken)
	{
		var usuarioId = _usuarioAtual.UsuarioId;

		if (string.IsNullOrWhiteSpace(usuarioId))
		{
			return Unauthorized();
		}

		var usuario = await _gestaoUsuarios.ObterAsync(usuarioId, cancellationToken);

		// O token ainda é válido, mas a conta foi excluída.
		if (usuario is null)
		{
			return Unauthorized();
		}

		var acessos = await _gestaoAcessos.ListarAsync(usuarioId, cancellationToken)
			?? Array.Empty<UsuarioAcessoDto>();

		return Ok(new
		{
			usuario,
			acessoIrrestrito = _usuarioAtual.PossuiAcessoIrrestrito,
			acessos
		});
	}
}
