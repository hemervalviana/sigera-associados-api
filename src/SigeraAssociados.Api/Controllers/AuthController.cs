using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SigeraAssociados.Api.DTOs.Auth;
using SigeraAssociados.Api.Seguranca;
using SigeraAssociados.Api.Services;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Infrastructure.Acesso;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Gerencia autenticação, confirmação de e-mail e recuperação de senha.
/// Não há cadastro público: novos usuários são criados somente por
/// Administradores e Gestores em POST /api/administracao/usuarios.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(LimiteRequisicoes.Autenticacao)]
public sealed class AuthController : ControllerBase
{
	private readonly UserManager<IdentityUser> _userManager;
	private readonly SignInManager<IdentityUser> _signInManager;
	private readonly IConfiguration _configuration;
	private readonly ILogger<AuthController> _logger;
	private readonly GmailEmailService _emailService;
	private readonly EmailConfirmacaoService _emailConfirmacao;
	private readonly GeradorTokenAcesso _geradorToken;
	private readonly ITokensRenovacaoService _tokensRenovacao;
	private readonly ISessoesService _sessoes;

	public AuthController(
		UserManager<IdentityUser> userManager,
		SignInManager<IdentityUser> signInManager,
		IConfiguration configuration,
		ILogger<AuthController> logger,
		GmailEmailService emailService,
		EmailConfirmacaoService emailConfirmacao,
		GeradorTokenAcesso geradorToken,
		ITokensRenovacaoService tokensRenovacao,
		ISessoesService sessoes)
	{
		_geradorToken = geradorToken;
		_tokensRenovacao = tokensRenovacao;
		_sessoes = sessoes;
		_userManager = userManager;
		_signInManager = signInManager;
		_configuration = configuration;
		_logger = logger;
		_emailService = emailService;
		_emailConfirmacao = emailConfirmacao;
	}

	/// <summary>
	/// Autentica um usuário e emite um token JWT.
	/// </summary>
	[HttpPost("login")]
	[ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status423Locked)]
	public async Task<IActionResult> Login(
		[FromBody] LoginRequest request,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var usuario = await _userManager.FindByEmailAsync(request.Email);

		// Não revela se o e-mail existe.
		if (usuario is null)
		{
			return this.Erro(
				StatusCodes.Status401Unauthorized,
				"E-mail ou senha inválidos, ou conta ainda não confirmada.");
		}

		// Verifica a senha e aplica a política de bloqueio do Identity.
		var resultado = await _signInManager.CheckPasswordSignInAsync(
			usuario,
			request.Senha,
			lockoutOnFailure: true);

		if (resultado.IsLockedOut)
		{
			return this.Erro(
				StatusCodes.Status423Locked,
				"Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.");
		}

		if (resultado.IsNotAllowed || !usuario.EmailConfirmed)
		{
			return this.Erro(
				StatusCodes.Status401Unauthorized,
				"E-mail ou senha inválidos, ou conta ainda não confirmada.");
		}

		if (!resultado.Succeeded)
		{
			return this.Erro(
				StatusCodes.Status401Unauthorized,
				"E-mail ou senha inválidos, ou conta ainda não confirmada.");
		}

		var resposta = await CriarRespostaDeLoginAsync(usuario, cancellationToken);

		_logger.LogInformation(
			"Autenticação concluída para o usuário {UsuarioId}.",
			usuario.Id);

		return Ok(resposta);
	}

	/// <summary>
	/// Troca o refreshToken por um novo token de acesso e um novo
	/// refreshToken, sem pedir a senha. O refreshToken usado deixa de valer.
	/// </summary>
	[HttpPost("renovar")]
	[ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Renovar(
		[FromBody] RenovarTokenRequest request,
		CancellationToken cancellationToken)
	{
		var resultado = await _tokensRenovacao.RenovarAsync(
			request.RefreshToken,
			cancellationToken);

		if (resultado.Status != StatusRenovacao.Sucesso)
		{
			if (resultado.Status == StatusRenovacao.Reutilizado)
			{
				_logger.LogWarning(
					"Reuso de refreshToken detectado para o usuário {UsuarioId}. Todas as sessões foram encerradas.",
					resultado.UsuarioId);

				if (resultado.UsuarioId is not null)
				{
					var afetado = await _userManager.FindByIdAsync(resultado.UsuarioId);

					if (afetado is not null)
					{
						await _userManager.UpdateSecurityStampAsync(afetado);
						_sessoes.InvalidarCache(afetado.Id);
					}
				}
			}

			return this.Erro(
				StatusCodes.Status401Unauthorized,
				"Sessão expirada ou inválida. Faça login novamente.",
				"sessao_invalida");
		}

		var usuario = await _userManager.FindByIdAsync(resultado.UsuarioId!);

		if (usuario is null)
		{
			return this.Erro(
				StatusCodes.Status401Unauthorized,
				"Sessão expirada ou inválida. Faça login novamente.",
				"sessao_invalida");
		}

		var tokenAcesso = await _geradorToken.GerarAsync(usuario);

		return Ok(new LoginResponse
		{
			AccessToken = tokenAcesso.Token,
			TokenType = "Bearer",
			ExpiresIn = tokenAcesso.ExpiraEmSegundos,
			RefreshToken = resultado.NovoToken!.Token,
			RefreshTokenExpiraEm = resultado.NovoToken.ExpiraEm,
			Email = usuario.Email ?? string.Empty
		});
	}

	/// <summary>
	/// Encerra a sessão: o token de acesso atual deixa de valer na hora
	/// e o refreshToken informado é revogado. Com todosOsDispositivos = true,
	/// encerra todas as sessões do usuário.
	/// </summary>
	[HttpPost("logout")]
	[Authorize]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Logout(
		[FromBody] LogoutRequest? request,
		CancellationToken cancellationToken)
	{
		var usuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

		if (string.IsNullOrWhiteSpace(usuarioId))
		{
			return this.Erro(StatusCodes.Status401Unauthorized, "Sessão inválida.");
		}

		if (request?.TodosOsDispositivos == true)
		{
			var usuario = await _userManager.FindByIdAsync(usuarioId);

			if (usuario is not null)
			{
				// Novo carimbo: todos os tokens de acesso deixam de valer.
				await _userManager.UpdateSecurityStampAsync(usuario);
			}

			await _tokensRenovacao.RevogarTodosAsync(
				usuarioId,
				MotivosRevogacao.LogoutTodos,
				cancellationToken);

			_sessoes.InvalidarCache(usuarioId);

			_logger.LogInformation(
				"Logout de todos os dispositivos do usuário {UsuarioId}.",
				usuarioId);

			return Ok(new { mensagem = "Sessões encerradas em todos os dispositivos." });
		}

		// Encerra só o token de acesso desta requisição.
		var tokenId = SessaoAtual.TokenId(HttpContext);
		var expiracao = SessaoAtual.Expiracao(HttpContext);

		if (!string.IsNullOrWhiteSpace(tokenId) && expiracao is not null)
		{
			_sessoes.EncerrarTokenAcesso(tokenId, expiracao.Value);
		}

		if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
		{
			await _tokensRenovacao.RevogarAsync(
				request.RefreshToken,
				usuarioId,
				cancellationToken);
		}

		_logger.LogInformation("Logout do usuário {UsuarioId}.", usuarioId);

		return Ok(new { mensagem = "Sessão encerrada." });
	}

	/// <summary>
	/// Monta a resposta de login: token de acesso + token de renovação.
	/// </summary>
	private async Task<LoginResponse> CriarRespostaDeLoginAsync(
		IdentityUser usuario,
		CancellationToken cancellationToken)
	{
		var tokenAcesso = await _geradorToken.GerarAsync(usuario);
		var renovacao = await _tokensRenovacao.EmitirAsync(usuario.Id, cancellationToken);

		return new LoginResponse
		{
			AccessToken = tokenAcesso.Token,
			TokenType = "Bearer",
			ExpiresIn = tokenAcesso.ExpiraEmSegundos,
			RefreshToken = renovacao.Token,
			RefreshTokenExpiraEm = renovacao.ExpiraEm,
			Email = usuario.Email ?? string.Empty
		};
	}

	/// <summary>
	/// Senha nova: encerra as sessões abertas com a senha antiga.
	/// O ResetPasswordAsync já troca o carimbo de segurança.
	/// </summary>
	private async Task EncerrarSessoesAposNovaSenhaAsync(
		IdentityUser usuario,
		CancellationToken cancellationToken)
	{
		await _tokensRenovacao.RevogarTodosAsync(
			usuario.Id,
			MotivosRevogacao.SenhaRedefinida,
			cancellationToken);

		_sessoes.InvalidarCache(usuario.Id);
	}

	/// <summary>
	/// Confirma o endereço de e-mail utilizando um token do Identity.
	/// </summary>
	[HttpPost("confirmar-email")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ConfirmarEmail(
		[FromBody] ConfirmarEmailRequest request,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var usuario = await _userManager.FindByEmailAsync(request.Email);

		// A mesma resposta é utilizada para evitar revelar contas existentes.
		if (usuario is null)
		{
			return this.Erro(
				StatusCodes.Status400BadRequest,
				"Não foi possível confirmar o e-mail. Verifique os dados e o token informado.");
		}

		if (usuario.EmailConfirmed)
		{
			return Ok(new
			{
				mensagem = "O endereço de e-mail já está confirmado."
			});
		}

		var resultado = await _userManager.ConfirmEmailAsync(
			usuario,
			request.Token);

		if (!resultado.Succeeded)
		{
			return this.Erro(
				StatusCodes.Status400BadRequest,
				"Não foi possível confirmar o e-mail. Verifique os dados e o token informado.");
		}

		_logger.LogInformation(
			"Endereço de e-mail confirmado para o usuário {UsuarioId}.",
			usuario.Id);

		return Ok(new
		{
			mensagem = "E-mail confirmado com sucesso."
		});
	}

	/// <summary>
	/// Confirma o e-mail pelo link enviado na mensagem de cadastro.
	/// O link aponta para a URL pública da API (cloudflared tunnel)
	/// e traz o token codificado em Base64Url.
	/// Devolve uma página HTML simples, pois é aberto no navegador.
	/// </summary>
	[HttpGet("confirmar-email")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ConfirmarEmailPorLink(
		[FromQuery] string? usuarioId,
		[FromQuery] string? token,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		const string mensagemFalha =
			"Não foi possível confirmar o e-mail. O link pode estar incompleto " +
			"ou expirado. Solicite um novo envio ao administrador do sistema.";

		if (string.IsNullOrWhiteSpace(usuarioId)
			|| string.IsNullOrWhiteSpace(token)
			|| usuarioId.Length > 450
			|| token.Length > 8192)
		{
			return PaginaResultado(
				StatusCodes.Status400BadRequest,
				"Link inválido",
				mensagemFalha);
		}

		var tokenDecodificado = EmailConfirmacaoService.DecodificarToken(token);

		if (tokenDecodificado is null)
		{
			return PaginaResultado(
				StatusCodes.Status400BadRequest,
				"Link inválido",
				mensagemFalha);
		}

		var usuario = await _userManager.FindByIdAsync(usuarioId);

		// A mesma resposta é utilizada para evitar revelar contas existentes.
		if (usuario is null)
		{
			return PaginaResultado(
				StatusCodes.Status400BadRequest,
				"Link inválido",
				mensagemFalha);
		}

		if (usuario.EmailConfirmed)
		{
			return PaginaResultado(
				StatusCodes.Status200OK,
				"E-mail já confirmado",
				"Seu endereço de e-mail já estava confirmado. Você já pode fazer login.");
		}

		var resultado = await _userManager.ConfirmEmailAsync(
			usuario,
			tokenDecodificado);

		if (!resultado.Succeeded)
		{
			return PaginaResultado(
				StatusCodes.Status400BadRequest,
				"Link inválido ou expirado",
				mensagemFalha);
		}

		_logger.LogInformation(
			"Endereço de e-mail confirmado pelo link para o usuário {UsuarioId}.",
			usuario.Id);

		return PaginaResultado(
			StatusCodes.Status200OK,
			"E-mail confirmado",
			"Seu endereço de e-mail foi confirmado com sucesso. Você já pode fazer login na Sigera Associados.");
	}

	/// <summary>
	/// Solicita a recuperação de senha e envia o token por e-mail.
	/// A resposta não revela se a conta existe.
	/// </summary>
	[HttpPost("solicitar-recuperacao-senha")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	public async Task<IActionResult> SolicitarRecuperacaoSenha(
		[FromBody] SolicitarRecuperacaoSenhaRequest request,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var mensagem = new
		{
			mensagem = "Se existir uma conta elegível para recuperação com esse endereço, você receberá as instruções por e-mail."
		};

		var usuario = await _userManager.FindByEmailAsync(request.Email);

		// Não envia tokens para contas inexistentes ou não confirmadas.
		if (usuario is null || !usuario.EmailConfirmed)
		{
			return Accepted(mensagem);
		}

		try
		{
			// Gera um token específico para redefinição de senha.
			var token = await _userManager.GeneratePasswordResetTokenAsync(
				usuario);

			// Com a URL pública (cloudflared), o e-mail traz o botão
			// "Redefinir senha"; sem ela, traz o token para uso manual.
			var link = _emailConfirmacao.CriarLink(
				EmailConfirmacaoService.RotaRedefinicaoSenha,
				usuario.Id,
				token);

			var corpoHtml = link is not null
				? CriarCorpoEmailComBotao(
					"Recuperação de senha",
					"Recebemos uma solicitação de recuperação de senha da sua conta Sigera Associados.",
					"Para criar uma nova senha, clique no botão abaixo. O link vale por 24 horas e só pode ser usado uma vez. Se você não pediu a recuperação, ignore esta mensagem.",
					"Redefinir senha",
					link)
				: CriarCorpoEmail(
					"Recuperação de senha",
					"Recebemos uma solicitação de recuperação de senha da sua conta Sigera Associados.",
					"Se você solicitou a recuperação, utilize o token abaixo no endpoint POST /api/auth/redefinir-senha, pelo Swagger (/swagger) ou por outro cliente de API. Se não solicitou, ignore esta mensagem.",
					token);

			await _emailService.EnviarAsync(
				usuario.Email!,
				"Recuperação de senha - Sigera Associados",
				corpoHtml,
				cancellationToken);

			_logger.LogInformation(
				"Solicitação de recuperação de senha processada para o usuário {UsuarioId}.",
				usuario.Id);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Mantém a resposta genérica para reduzir a exposição da conta.
			_logger.LogError(
				"Falha no envio de recuperação de senha para o usuário {UsuarioId}. Tipo de erro: {TipoErro}.",
				usuario.Id,
				ex.GetType().Name);
		}

		return Accepted(mensagem);
	}

	/// <summary>
	/// Abre o formulário de nova senha a partir do link do e-mail
	/// de recuperação. Só mostra o formulário se o link for válido.
	/// </summary>
	[HttpGet("redefinir-senha")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> FormularioRedefinicaoSenha(
		[FromQuery] string? usuarioId,
		[FromQuery] string? token,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!LinkComFormatoValido(usuarioId, token, out _))
		{
			return PaginaLinkInvalido();
		}

		var usuario = await ObterUsuarioComTokenDeSenhaValidoAsync(usuarioId!, token!);

		if (usuario is null)
		{
			return PaginaLinkInvalido();
		}

		return PaginaFormularioSenha(StatusCodes.Status200OK, usuarioId!, token!, null);
	}

	/// <summary>
	/// Recebe o formulário de nova senha (enviado pela página do GET
	/// redefinir-senha) e troca a senha. Responde com páginas HTML.
	/// </summary>
	[HttpPost("redefinir-senha/formulario")]
	[Consumes("application/x-www-form-urlencoded")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> RedefinirSenhaPorFormulario(
		[FromForm] string? usuarioId,
		[FromForm] string? token,
		[FromForm] string? novaSenha,
		[FromForm] string? confirmarNovaSenha,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!LinkComFormatoValido(usuarioId, token, out var tokenDecodificado))
		{
			return PaginaLinkInvalido();
		}

		// Regras básicas antes de consultar o banco.
		if (string.IsNullOrEmpty(novaSenha) || novaSenha != confirmarNovaSenha)
		{
			return PaginaFormularioSenha(
				StatusCodes.Status400BadRequest,
				usuarioId!,
				token!,
				"A senha e a confirmação não coincidem.");
		}

		if (novaSenha.Length is < 12 or > 128)
		{
			return PaginaFormularioSenha(
				StatusCodes.Status400BadRequest,
				usuarioId!,
				token!,
				"A senha deve ter entre 12 e 128 caracteres.");
		}

		var usuario = await _userManager.FindByIdAsync(usuarioId!);

		if (usuario is null)
		{
			return PaginaLinkInvalido();
		}

		var resultado = await _userManager.ResetPasswordAsync(
			usuario,
			tokenDecodificado!,
			novaSenha);

		if (!resultado.Succeeded)
		{
			if (resultado.Errors.Any(e => e.Code == "InvalidToken"))
			{
				return PaginaLinkInvalido();
			}

			return PaginaFormularioSenha(
				StatusCodes.Status400BadRequest,
				usuarioId!,
				token!,
				TraduzirErrosDeSenha(resultado.Errors));
		}

		// Senha nova: zera as tentativas erradas de login.
		await _userManager.ResetAccessFailedCountAsync(usuario);

		await EncerrarSessoesAposNovaSenhaAsync(usuario, cancellationToken);

		_logger.LogInformation(
			"Senha redefinida pelo link de recuperação para o usuário {UsuarioId}.",
			usuario.Id);

		return PaginaResultado(
			StatusCodes.Status200OK,
			"Senha redefinida",
			"Sua senha foi alterada com sucesso. Você já pode fazer login com a nova senha.");
	}

	/// <summary>
	/// Redefine a senha com um token válido do Identity.
	/// </summary>
	[HttpPost("redefinir-senha")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> RedefinirSenha(
		[FromBody] RedefinirSenhaRequest request,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var usuario = await _userManager.FindByEmailAsync(request.Email);

		if (usuario is null)
		{
			return this.Erro(
				StatusCodes.Status400BadRequest,
				"Não foi possível redefinir a senha. Verifique os dados e o token informado.");
		}

		var resultado = await _userManager.ResetPasswordAsync(
			usuario,
			request.Token,
			request.NovaSenha);

		if (!resultado.Succeeded)
		{
			// Não retorna detalhes internos nem informações sensíveis da conta.
			return this.Erro(
				StatusCodes.Status400BadRequest,
				"Não foi possível redefinir a senha. Verifique os dados e o token informado.");
		}

		await EncerrarSessoesAposNovaSenhaAsync(usuario, cancellationToken);

		_logger.LogInformation(
			"Senha redefinida para o usuário {UsuarioId}.",
			usuario.Id);

		return Ok(new
		{
			mensagem = "Senha redefinida com sucesso."
		});
	}

	/// <summary>
	/// Confere o formato do link (sem acessar o banco) e decodifica o token.
	/// </summary>
	private static bool LinkComFormatoValido(
		string? usuarioId,
		string? token,
		out string? tokenDecodificado)
	{
		tokenDecodificado = null;

		if (string.IsNullOrWhiteSpace(usuarioId)
			|| string.IsNullOrWhiteSpace(token)
			|| usuarioId.Length > 450
			|| token.Length > 8192)
		{
			return false;
		}

		tokenDecodificado = EmailConfirmacaoService.DecodificarToken(token);

		return tokenDecodificado is not null;
	}

	/// <summary>
	/// Devolve o usuário quando o token de redefinição ainda é válido
	/// (não expirou e não foi usado). Não troca a senha.
	/// </summary>
	private async Task<IdentityUser?> ObterUsuarioComTokenDeSenhaValidoAsync(
		string usuarioId,
		string tokenCodificado)
	{
		var token = EmailConfirmacaoService.DecodificarToken(tokenCodificado);
		var usuario = await _userManager.FindByIdAsync(usuarioId);

		if (usuario is null || token is null)
		{
			return null;
		}

		var valido = await _userManager.VerifyUserTokenAsync(
			usuario,
			_userManager.Options.Tokens.PasswordResetTokenProvider,
			UserManager<IdentityUser>.ResetPasswordTokenPurpose,
			token);

		return valido ? usuario : null;
	}

	/// <summary>
	/// Converte os erros de senha do Identity em mensagens em português.
	/// </summary>
	private static string TraduzirErrosDeSenha(IEnumerable<IdentityError> erros)
	{
		var mensagens = erros
			.Select(erro => erro.Code switch
			{
				"PasswordTooShort" => "A senha deve ter pelo menos 12 caracteres.",
				"PasswordRequiresDigit" => "A senha precisa ter pelo menos um número.",
				"PasswordRequiresLower" => "A senha precisa ter pelo menos uma letra minúscula.",
				"PasswordRequiresUpper" => "A senha precisa ter pelo menos uma letra maiúscula.",
				"PasswordRequiresNonAlphanumeric" => "A senha precisa ter pelo menos um símbolo (ex.: ! @ # $).",
				"PasswordRequiresUniqueChars" => "A senha precisa ter mais caracteres diferentes.",
				_ => "Não foi possível redefinir a senha."
			})
			.Distinct()
			.ToArray();

		return string.Join(" ", mensagens);
	}

	private static ContentResult PaginaLinkInvalido()
	{
		return PaginaResultado(
			StatusCodes.Status400BadRequest,
			"Link inválido ou expirado",
			"Este link de recuperação de senha é inválido, expirou ou já foi usado. Peça uma nova recuperação de senha.");
	}

	/// <summary>
	/// Página com o formulário de nova senha.
	/// O usuário e o token seguem em campos ocultos para o POST.
	/// </summary>
	private static ContentResult PaginaFormularioSenha(
		int statusCode,
		string usuarioId,
		string tokenCodificado,
		string? erro)
	{
		var codificador = HtmlEncoder.Default;

		var usuarioSeguro = codificador.Encode(usuarioId);
		var tokenSeguro = codificador.Encode(tokenCodificado);

		var blocoErro = erro is null
			? string.Empty
			: $"""<p style="background: #fdecea; color: #a50e0e; padding: 12px; border-radius: 6px;">{codificador.Encode(erro)}</p>""";

		var corpo = $"""
            {blocoErro}
            <form method="post" action="/api/auth/redefinir-senha/formulario">
                <input type="hidden" name="usuarioId" value="{usuarioSeguro}">
                <input type="hidden" name="token" value="{tokenSeguro}">
                <p>
                    <label for="novaSenha"><strong>Nova senha</strong></label><br>
                    <input id="novaSenha" name="novaSenha" type="password" required minlength="12" maxlength="128"
                           autocomplete="new-password" style="width: 100%; padding: 10px; box-sizing: border-box;">
                </p>
                <p>
                    <label for="confirmarNovaSenha"><strong>Confirme a nova senha</strong></label><br>
                    <input id="confirmarNovaSenha" name="confirmarNovaSenha" type="password" required minlength="12" maxlength="128"
                           autocomplete="new-password" style="width: 100%; padding: 10px; box-sizing: border-box;">
                </p>
                <p style="font-size: 13px; color: #555;">
                    Mínimo de 12 caracteres, com letra maiúscula, letra minúscula, número e símbolo.
                </p>
                <button type="submit"
                        style="background: #1f6feb; color: #fff; padding: 12px 24px; border: 0; border-radius: 6px; font-weight: bold; cursor: pointer;">
                    Salvar nova senha
                </button>
            </form>
            """;

		return PaginaHtml(statusCode, "Redefinir senha", corpo);
	}

	/// <summary>
	/// Monta a página HTML de resultado (confirmação de e-mail e senha).
	/// </summary>
	private static ContentResult PaginaResultado(
		int statusCode,
		string titulo,
		string mensagem)
	{
		var mensagemSegura = HtmlEncoder.Default.Encode(mensagem);

		return PaginaHtml(statusCode, titulo, $"<p>{mensagemSegura}</p>");
	}

	/// <summary>
	/// Layout comum das páginas HTML da API.
	/// O corpo já deve chegar com os dados dinâmicos codificados.
	/// </summary>
	private static ContentResult PaginaHtml(
		int statusCode,
		string titulo,
		string corpoSeguro)
	{
		var tituloSeguro = HtmlEncoder.Default.Encode(titulo);

		return new ContentResult
		{
			StatusCode = statusCode,
			ContentType = "text/html; charset=utf-8",
			Content = $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <meta name="robots" content="noindex">
                    <title>{tituloSeguro} - Sigera Associados</title>
                </head>
                <body style="font-family: Arial, sans-serif; color: #222; line-height: 1.6; background: #f6f7f9;">
                    <main style="max-width: 520px; margin: 64px auto; padding: 32px; background: #fff; border-radius: 8px;">
                        <h1 style="font-size: 22px; margin-top: 0;">Sigera Associados</h1>
                        <h2 style="font-size: 18px;">{tituloSeguro}</h2>
                        {corpoSeguro}
                    </main>
                </body>
                </html>
                """
		};
	}

	/// <summary>
	/// Corpo HTML do e-mail com um botão de ação (link público).
	/// </summary>
	private static string CriarCorpoEmailComBotao(
		string titulo,
		string introducao,
		string instrucoes,
		string textoBotao,
		string link)
	{
		var codificador = HtmlEncoder.Default;

		var tituloSeguro = codificador.Encode(titulo);
		var introducaoSegura = codificador.Encode(introducao);
		var instrucoesSeguras = codificador.Encode(instrucoes);
		var textoBotaoSeguro = codificador.Encode(textoBotao);
		var linkSeguro = codificador.Encode(link);

		return $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{tituloSeguro}</title>
            </head>
            <body style="font-family: Arial, sans-serif; color: #222; line-height: 1.6;">
                <main style="max-width: 600px; margin: 0 auto; padding: 24px;">
                    <h1 style="font-size: 24px;">Sigera Associados</h1>
                    <h2 style="font-size: 20px;">{tituloSeguro}</h2>
                    <p>{introducaoSegura}</p>
                    <p>{instrucoesSeguras}</p>
                    <p style="margin: 24px 0;">
                        <a href="{linkSeguro}"
                           style="background: #1f6feb; color: #fff; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: bold;">
                            {textoBotaoSeguro}
                        </a>
                    </p>
                    <p style="font-size: 13px; color: #555;">
                        Se o botão não funcionar, copie e cole este endereço no navegador:<br>
                        <span style="overflow-wrap: anywhere;">{linkSeguro}</span>
                    </p>
                </main>
            </body>
            </html>
            """;
	}

	/// <summary>
	/// Monta o corpo HTML do e-mail e codifica os dados dinâmicos
	/// para evitar a inserção de HTML não confiável na mensagem.
	/// </summary>
	private static string CriarCorpoEmail(
		string titulo,
		string introducao,
		string instrucoes,
		string token)
	{
		var codificador = HtmlEncoder.Default;

		var tituloSeguro = codificador.Encode(titulo);
		var introducaoSegura = codificador.Encode(introducao);
		var instrucoesSeguras = codificador.Encode(instrucoes);
		var tokenSeguro = codificador.Encode(token);

		return $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{tituloSeguro}</title>
            </head>
            <body style="font-family: Arial, sans-serif; color: #222; line-height: 1.6;">
                <main style="max-width: 600px; margin: 0 auto; padding: 24px;">
                    <h1 style="font-size: 24px;">Sigera Associados</h1>
                    <h2 style="font-size: 20px;">{tituloSeguro}</h2>
                    <p>{introducaoSegura}</p>
                    <p>{instrucoesSeguras}</p>
                    <div style="padding: 16px; background: #f3f4f6; border-radius: 8px; overflow-wrap: anywhere;">
                        <strong>Seu token:</strong>
                        <pre style="white-space: pre-wrap; overflow-wrap: anywhere;">{tokenSeguro}</pre>
                    </div>
                    <p style="font-size: 13px; color: #555;">
                        Por segurança, não compartilhe este token com outras pessoas.
                        Se você não solicitou esta operação, ignore esta mensagem.
                    </p>
                </main>
            </body>
            </html>
            """;
	}
}