using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Api.DTOs.Usuarios;
using SigeraAssociados.Application.Interfaces.Usuarios;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Consulta e manutenção de usuários: listar, consultar, trocar grupo,
/// incluir e remover de grupos, bloquear, desbloquear e excluir.
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
	/// Inclui o usuário em mais um grupo, mantendo os grupos que ele já tem.
	/// </summary>
	/// <remarks>
	/// Diferente de `PUT /api/administracao/usuarios/{id}/grupo`, que substitui
	/// todos os grupos pelo informado.
	///
	/// Regras: ninguém altera a própria conta; Gestores não alteram contas de
	/// administradores; o grupo Administradores não é concedido por aqui.
	///
	/// Vale na hora: o token de acesso atual do usuário deixa de valer e a
	/// próxima renovação (`POST /api/auth/renovar`) já traz o grupo novo.
	///
	/// Exemplo:
	///
	///     POST /api/administracao/usuarios/{id}/grupos
	///     {
	///       "grupo": "Financeiro"
	///     }
	/// </remarks>
	/// <param name="id">Identificador do usuário.</param>
	/// <param name="request">Grupo em que o usuário será incluído.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Usuário incluído no grupo; devolve o usuário com os grupos atualizados.</response>
	/// <response code="400">Grupo vazio, inexistente ou Administradores.</response>
	/// <response code="403">Própria conta ou, sendo Gestor, conta de administrador.</response>
	/// <response code="404">Usuário não encontrado.</response>
	/// <response code="409">O usuário já pertence ao grupo.</response>
	[HttpPost("{id}/grupos")]
	[Consumes("application/json")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AdicionarGrupo(
		string id,
		[FromBody] AdicionarGrupoRequest request,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoUsuarios.AdicionarGrupoAsync(
			id,
			request.Grupo,
			cancellationToken);

		return ParaResposta(
			resultado,
			"Grupo incluído",
			"Usuário incluído no grupo.");
	}

	/// <summary>
	/// Remove o usuário de um grupo, mantendo os demais grupos.
	/// </summary>
	/// <remarks>
	/// O usuário precisa continuar em pelo menos um grupo; para trocar o único
	/// grupo, use `PUT /api/administracao/usuarios/{id}/grupo`.
	///
	/// Regras: ninguém altera a própria conta; Gestores não alteram contas de
	/// administradores (por isso só administradores removem o grupo
	/// Administradores de alguém).
	///
	/// Vale na hora, como a inclusão.
	///
	/// Nomes com espaço ou `&amp;` vão codificados na URL. Exemplo:
	///
	///     DELETE /api/administracao/usuarios/{id}/grupos/Financeiro
	///     DELETE /api/administracao/usuarios/{id}/grupos/Coordenadora%20Adm.%20%26%20Operacional
	/// </remarks>
	/// <param name="id">Identificador do usuário.</param>
	/// <param name="grupo">Nome do grupo (não diferencia maiúsculas).</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Usuário removido do grupo; devolve o usuário com os grupos atualizados.</response>
	/// <response code="400">Grupo inexistente ou é o único grupo do usuário.</response>
	/// <response code="403">Própria conta ou, sendo Gestor, conta de administrador.</response>
	/// <response code="404">Usuário não encontrado.</response>
	/// <response code="409">O usuário não pertence ao grupo.</response>
	[HttpDelete("{id}/grupos/{grupo}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> RemoverGrupo(
		string id,
		string grupo,
		CancellationToken cancellationToken)
	{
		var resultado = await _gestaoUsuarios.RemoverGrupoAsync(
			id,
			grupo,
			cancellationToken);

		return ParaResposta(
			resultado,
			"Grupo removido",
			"Usuário removido do grupo.");
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
			StatusGestaoUsuario.Conflito => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status500InternalServerError
		};

		return this.Erro(
			status,
			resultado.Mensagem ?? RespostasErro.Titulo(status));
	}
}
