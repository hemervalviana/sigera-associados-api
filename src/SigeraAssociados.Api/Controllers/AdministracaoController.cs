using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Api.DTOs.Administracao;
using SigeraAssociados.Api.Seguranca;
using SigeraAssociados.Api.Services;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Cria usuários e lista os grupos funcionais.
/// Somente Administradores e Gestores podem criar usuários;
/// não existe cadastro público.
/// </summary>
[ApiController]
[Route("api/administracao")]
[Authorize(Roles = Grupos.PerfisGestaoUsuarios)]
public sealed class AdministracaoController : ControllerBase
{
	private readonly UserManager<IdentityUser> _userManager;
	private readonly RoleManager<IdentityRole> _roleManager;
	private readonly EmailConfirmacaoService _emailConfirmacao;
	private readonly ILogger<AdministracaoController> _logger;

	public AdministracaoController(
		UserManager<IdentityUser> userManager,
		RoleManager<IdentityRole> roleManager,
		EmailConfirmacaoService emailConfirmacao,
		ILogger<AdministracaoController> logger)
	{
		_userManager = userManager;
		_roleManager = roleManager;
		_emailConfirmacao = emailConfirmacao;
		_logger = logger;
	}

	/// <summary>
	/// Lista os sete grupos funcionais oficiais do sistema.
	/// </summary>
	[HttpGet("grupos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> ListarGrupos(
		CancellationToken cancellationToken)
	{
		var nomesGrupos = Grupos.Funcionais.ToArray();

		var grupos = await _roleManager.Roles
			.Where(grupo =>
				grupo.Name != null &&
				nomesGrupos.Contains(grupo.Name))
			.OrderBy(grupo => grupo.Name)
			.Select(grupo => new
			{
				grupo.Id,
				Nome = grupo.Name
			})
			.ToListAsync(cancellationToken);

		return Ok(grupos);
	}

	/// <summary>
	/// Cria um usuário, atribui um grupo funcional permitido
	/// e tenta enviar o e-mail de confirmação.
	/// </summary>
	[HttpPost("usuarios")]
	[EnableRateLimiting(LimiteRequisicoes.EnvioEmail)]
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
	public async Task<IActionResult> CriarUsuario(
		[FromBody] CriarUsuarioRequest request,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var email = request.Email.Trim();
		// Impede a atribuição de privilégios administrativos
		// por meio do cadastro geral de usuários.
		var grupoPermitido = Grupos.Normalizar(request.Grupo);

		if (grupoPermitido is null ||
			grupoPermitido == Grupos.Administradores)
		{
			return BadRequest(new
			{
				mensagem =
					"Informe um grupo funcional válido. " +
					"A atribuição do grupo Administradores exige um processo separado."
			});
		}

		// Impede duplicidade de contas.
		var usuarioExistente = await _userManager.FindByEmailAsync(email);

		if (usuarioExistente is not null)
		{
			return Conflict(new
			{
				mensagem = "Já existe uma conta cadastrada com esse e-mail."
			});
		}

		// Verifica se o grupo já foi provisionado no Identity.
		var grupo = await _roleManager.FindByNameAsync(grupoPermitido);

		if (grupo is null)
		{
			_logger.LogError(
				"O grupo funcional {Grupo} não foi encontrado no Identity.",
				grupoPermitido);

			return StatusCode(
				StatusCodes.Status500InternalServerError,
				new
				{
					mensagem =
						"O grupo solicitado ainda não foi provisionado no sistema."
				});
		}

		// Cria a conta sem confirmar automaticamente o e-mail.
		var usuario = new IdentityUser
		{
			UserName = email,
			Email = email,
			EmailConfirmed = false
		};

		var resultadoCriacao = await _userManager.CreateAsync(
			usuario,
			request.Senha);

		if (!resultadoCriacao.Succeeded)
		{
			var codigosErro = resultadoCriacao.Errors
				.Select(erro => erro.Code)
				.ToArray();

			return BadRequest(new
			{
				mensagem =
					"Não foi possível criar a conta. " +
					"Verifique os dados e os requisitos de senha.",
				codigosErro
			});
		}

		// Atribui exclusivamente o grupo funcional autorizado.
		var resultadoGrupo = await _userManager.AddToRoleAsync(
			usuario,
			grupoPermitido);

		if (!resultadoGrupo.Succeeded)
		{
			// Evita deixar uma conta criada sem o grupo solicitado.
			var resultadoExclusao = await _userManager.DeleteAsync(usuario);

			if (!resultadoExclusao.Succeeded)
			{
				_logger.LogCritical(
					"Falha ao atribuir grupo e remover a conta parcialmente criada. " +
					"UsuarioId: {UsuarioId}.",
					usuario.Id);
			}
			else
			{
				_logger.LogError(
					"Falha ao atribuir grupo. A conta recém-criada foi removida.");
			}

			return StatusCode(
				StatusCodes.Status500InternalServerError,
				new
				{
					mensagem =
						"Não foi possível concluir a criação da conta."
				});
		}

		// A conta e o grupo já foram persistidos.
		// Se o SMTP falhar, preservamos a conta para permitir o reenvio.
		var emailEnviado = await TentarEnviarConfirmacaoAsync(
			usuario,
			cancellationToken);

		_logger.LogInformation(
			"Conta criada por um administrador. " +
			"UsuarioId: {UsuarioId}; Grupo: {Grupo}; EmailEnviado: {EmailEnviado}.",
			usuario.Id,
			grupoPermitido,
			emailEnviado);

		return StatusCode(
			StatusCodes.Status201Created,
			new
			{
				mensagem = emailEnviado
					? "Conta criada. O e-mail de confirmação foi enviado. A confirmação continua necessária para realizar o login."
					: "Conta criada, mas não foi possível enviar o e-mail de confirmação. O administrador poderá solicitar um novo envio.",
				usuario = new
				{
					usuario.Id,
					usuario.Email,
					Grupo = grupoPermitido,
					usuario.EmailConfirmed
				},
				emailConfirmacaoEnviado = emailEnviado
			});
	}

	/// <summary>
	/// Reenvia a confirmação de e-mail de uma conta existente
	/// que ainda não foi confirmada.
	/// </summary>
	[HttpPost("usuarios/{id}/reenviar-confirmacao")]
	[EnableRateLimiting(LimiteRequisicoes.EnvioEmail)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
	public async Task<IActionResult> ReenviarConfirmacao(
		string id,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var usuario = await _userManager.FindByIdAsync(id);

		if (usuario is null)
		{
			return NotFound(new
			{
				mensagem = "Usuário não encontrado."
			});
		}

		if (usuario.EmailConfirmed)
		{
			return BadRequest(new
			{
				mensagem = "O endereço de e-mail deste usuário já está confirmado."
			});
		}

		var emailEnviado = await TentarEnviarConfirmacaoAsync(
			usuario,
			cancellationToken);

		if (!emailEnviado)
		{
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new
				{
					mensagem =
						"Não foi possível enviar o e-mail de confirmação. " +
						"Verifique a configuração do serviço de e-mail e tente novamente."
				});
		}

		return Ok(new
		{
			mensagem = "E-mail de confirmação enviado com sucesso.",
			usuario = new
			{
				usuario.Id,
				usuario.Email,
				usuario.EmailConfirmed
			}
		});
	}

	/// <summary>
	/// Gera um token oficial do Identity e envia o link de confirmação por e-mail.
	/// O token não é incluído na resposta HTTP nem nos logs.
	/// </summary>
	private async Task<bool> TentarEnviarConfirmacaoAsync(
		IdentityUser usuario,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(usuario.Email))
		{
			_logger.LogWarning(
				"Não foi possível enviar confirmação: usuário {UsuarioId} sem e-mail.",
				usuario.Id);

			return false;
		}

		try
		{
			// Gera o token do Identity e envia o link de confirmação
			// (URL pública do cloudflared em UrlPublica:BaseUrl).
			await _emailConfirmacao.EnviarAsync(
				usuario,
				cancellationToken);

			_logger.LogInformation(
				"E-mail de confirmação enviado para o usuário {UsuarioId}.",
				usuario.Id);

			return true;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Não registra o token nem as credenciais SMTP.
			_logger.LogError(
				"Falha ao enviar confirmação para o usuário {UsuarioId}. " +
				"Tipo de erro: {TipoErro}.",
				usuario.Id,
				ex.GetType().Name);

			return false;
		}
	}
}