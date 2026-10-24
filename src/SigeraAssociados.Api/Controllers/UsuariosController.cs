using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Api.DTOs.Usuarios;
using SigeraAssociados.Application.Interfaces.Usuarios;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Consulta e manutenção de usuários: listar, consultar, trocar grupo,
/// bloquear, desbloquear e excluir.
/// Disponível para Administradores e Gestores. Somente administradores
/// alteram contas de administradores, e ninguém altera a própria conta.
/// A criação de usuários fica em AdministracaoController.
/// </summary>
[ApiController]
[Route("api/administracao/usuarios")]
[Authorize(Roles = Grupos.PerfisGestaoUsuarios)]
public sealed class UsuariosController : ControllerBase
{
	private readonly IGestaoUsuariosService _gestaoUsuarios;
	private readonly ILogger<UsuariosController> _logger;

	public UsuariosController(
		IGestaoUsuariosService gestaoUsuarios,
		ILogger<UsuariosController> logger)
	{
		_gestaoUsuarios = gestaoUsuarios;
		_logger = logger;
	}

	/// <summary>
	/// Lista usuários com filtro por trecho do e-mail e por grupo.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Listar(
		[FromQuery] string? busca,
		[FromQuery] string? grupo,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 20,
		CancellationToken cancellationToken = default)
	{
		// Filtros inválidos viram 400 pelo tratador global (DadosInvalidosException).
		var resultado = await _gestaoUsuarios.ListarAsync(
			new ConsultaUsuariosRequest
			{
				Busca = busca,
				Grupo = grupo,
				Pagina = pagina,
				TamanhoPagina = tamanhoPagina
			},
			cancellationToken);

		return Ok(new
		{
			paginacao = new
			{
				pagina = resultado.Pagina,
				tamanhoPagina = resultado.TamanhoPagina,
				totalRegistros = resultado.TotalRegistros,
				totalPaginas = resultado.TotalPaginas
			},
			quantidade = resultado.Usuarios.Count,
			usuarios = resultado.Usuarios
		});
	}

	/// <summary>
	/// Consulta um usuário pelo identificador.
	/// </summary>
	[HttpGet("{id}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Obter(
		string id,
		CancellationToken cancellationToken)
	{
		var usuario = await _gestaoUsuarios.ObterAsync(id, cancellationToken);

		return usuario is null
			? this.Erro(
				StatusCodes.Status404NotFound,
				"Usuário não encontrado.")
			: Ok(usuario);
	}

	/// <summary>
	/// Substitui o grupo funcional do usuário.
	/// O novo grupo vale a partir do próximo login (o token atual
	/// mantém os grupos antigos até expirar).
	/// </summary>
	[HttpPut("{id}/grupo")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarGrupo(
		string id,
		[FromBody] AlterarGrupoRequest request,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoUsuarios.AlterarGrupoAsync(
			id,
			request.Grupo,
			cancellationToken);

		return ParaResposta(resultado, "Grupo alterado", "Grupo alterado.");
	}

	/// <summary>
	/// Bloqueia o login do usuário por tempo indeterminado.
	/// </summary>
	[HttpPost("{id}/bloquear")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Bloquear(
		string id,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoUsuarios.BloquearAsync(id, cancellationToken);

		return ParaResposta(
			resultado,
			"Usuário bloqueado",
			"Usuário bloqueado. Tokens já emitidos continuam válidos até expirar.");
	}

	/// <summary>
	/// Remove o bloqueio do usuário.
	/// </summary>
	[HttpPost("{id}/desbloquear")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Desbloquear(
		string id,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoUsuarios.DesbloquearAsync(id, cancellationToken);

		return ParaResposta(resultado, "Usuário desbloqueado", "Usuário desbloqueado.");
	}

	/// <summary>
	/// Exclui o usuário, seus grupos e seus acessos.
	/// </summary>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(
		string id,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoUsuarios.ExcluirAsync(id, cancellationToken);

		return ParaResposta(resultado, "Usuário excluído", "Usuário excluído.");
	}

	/// <summary>
	/// Converte o resultado do serviço em resposta HTTP
	/// e registra a operação concluída.
	/// </summary>
	private IActionResult ParaResposta(
		ResultadoGestaoUsuario resultado,
		string operacao,
		string mensagemSucesso)
	{
		if (resultado.Status == StatusGestaoUsuario.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: usuário {UsuarioId} por {Executor}.",
				operacao,
				resultado.Usuario?.Id,
				User.Identity?.Name ?? "(desconhecido)");

			return Ok(new
			{
				mensagem = mensagemSucesso,
				usuario = resultado.Usuario
			});
		}

		var status = resultado.Status switch
		{
			StatusGestaoUsuario.UsuarioNaoEncontrado => StatusCodes.Status404NotFound,
			StatusGestaoUsuario.Proibido => StatusCodes.Status403Forbidden,
			StatusGestaoUsuario.Invalido => StatusCodes.Status400BadRequest,
			_ => StatusCodes.Status500InternalServerError
		};

		return this.Erro(
			status,
			resultado.Mensagem ?? RespostasErro.Titulo(status));
	}
}
