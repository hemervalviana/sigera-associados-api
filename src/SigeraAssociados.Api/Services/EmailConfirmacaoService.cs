using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace SigeraAssociados.Api.Services;

/// <summary>
/// Gera o token de confirmação de e-mail do Identity e envia a mensagem
/// com o link de confirmação.
///
/// O link é montado com a URL pública configurada na chave
/// UrlPublica:BaseUrl — por exemplo, a URL criada pelo cloudflared tunnel
/// (https://xxxx.trycloudflare.com). Quando a chave não está configurada,
/// a mensagem é enviada somente com o token, como antes.
/// </summary>
public sealed class EmailConfirmacaoService
{
	/// <summary>
	/// Chave de configuração com a URL pública da API.
	/// </summary>
	public const string ChaveUrlPublica = "UrlPublica:BaseUrl";

	/// <summary>
	/// Rota do endpoint GET que confirma o e-mail pelo link.
	/// </summary>
	public const string RotaConfirmacao = "api/auth/confirmar-email";

	private readonly UserManager<IdentityUser> _userManager;
	private readonly GmailEmailService _emailService;
	private readonly IConfiguration _configuration;
	private readonly ILogger<EmailConfirmacaoService> _logger;

	public EmailConfirmacaoService(
		UserManager<IdentityUser> userManager,
		GmailEmailService emailService,
		IConfiguration configuration,
		ILogger<EmailConfirmacaoService> logger)
	{
		_userManager = userManager;
		_emailService = emailService;
		_configuration = configuration;
		_logger = logger;
	}

	/// <summary>
	/// Gera um novo token de confirmação e envia o e-mail ao usuário.
	/// O token não é registrado em log nem devolvido na resposta HTTP.
	/// </summary>
	public async Task EnviarAsync(
		IdentityUser usuario,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(usuario);

		if (string.IsNullOrWhiteSpace(usuario.Email))
		{
			throw new InvalidOperationException(
				"O usuário não possui e-mail cadastrado.");
		}

		var token = await _userManager.GenerateEmailConfirmationTokenAsync(
			usuario);

		var link = CriarLink(usuario.Id, token);

		if (link is not null)
		{
			_logger.LogInformation(
				"Link de confirmação gerado com a URL pública {UrlPublica}.",
				_configuration[ChaveUrlPublica]);
		}
		else
		{
			_logger.LogWarning(
				"A chave {Chave} não está configurada ou é inválida. " +
				"O e-mail de confirmação será enviado somente com o token.",
				ChaveUrlPublica);
		}

		await _emailService.EnviarAsync(
			usuario.Email,
			"Confirme seu cadastro - Sigera Associados",
			CriarCorpoEmail(token, link),
			cancellationToken);
	}

	/// <summary>
	/// Monta o link público de confirmação.
	/// O token do Identity é codificado em Base64Url para trafegar
	/// com segurança na query string.
	/// Retorna null quando a URL pública não está configurada.
	/// </summary>
	public string? CriarLink(string usuarioId, string token)
	{
		var urlPublica = ObterUrlPublica();

		if (urlPublica is null)
		{
			return null;
		}

		var tokenCodificado = WebEncoders.Base64UrlEncode(
			Encoding.UTF8.GetBytes(token));

		return QueryHelpers.AddQueryString(
			new Uri(urlPublica, RotaConfirmacao).ToString(),
			new Dictionary<string, string?>
			{
				["usuarioId"] = usuarioId,
				["token"] = tokenCodificado
			});
	}

	/// <summary>
	/// Decodifica o token recebido pelo link.
	/// Retorna null quando o valor não é Base64Url válido.
	/// </summary>
	public static string? DecodificarToken(string tokenCodificado)
	{
		try
		{
			return Encoding.UTF8.GetString(
				WebEncoders.Base64UrlDecode(tokenCodificado));
		}
		catch (FormatException)
		{
			return null;
		}
	}

	/// <summary>
	/// Lê a URL pública a cada envio, para aceitar uma nova URL
	/// do cloudflared sem reiniciar a API.
	/// Exige HTTPS; HTTP só é aceito para localhost.
	/// </summary>
	private Uri? ObterUrlPublica()
	{
		var valor = _configuration[ChaveUrlPublica]?.Trim();

		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		if (!valor.EndsWith('/'))
		{
			valor += "/";
		}

		if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri))
		{
			return null;
		}

		var esquemaPermitido =
			uri.Scheme == Uri.UriSchemeHttps
			|| (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

		if (!esquemaPermitido
			|| !string.IsNullOrEmpty(uri.Query)
			|| !string.IsNullOrEmpty(uri.Fragment))
		{
			return null;
		}

		return uri;
	}

	/// <summary>
	/// Monta o corpo HTML do e-mail.
	/// Com link: botão de confirmação. Sem link: token para uso manual.
	/// </summary>
	private static string CriarCorpoEmail(string token, string? link)
	{
		var codificador = HtmlEncoder.Default;

		string instrucoes;

		if (link is not null)
		{
			var linkSeguro = codificador.Encode(link);

			instrucoes = $"""
                <p>Para confirmar seu endereço de e-mail, clique no botão abaixo:</p>
                <p style="margin: 24px 0;">
                    <a href="{linkSeguro}"
                       style="background: #1f6feb; color: #fff; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: bold;">
                        Confirmar e-mail
                    </a>
                </p>
                <p style="font-size: 13px; color: #555;">
                    Se o botão não funcionar, copie e cole este endereço no navegador:<br>
                    <span style="overflow-wrap: anywhere;">{linkSeguro}</span>
                </p>
                """;
		}
		else
		{
			var tokenSeguro = codificador.Encode(token);

			instrucoes = $"""
                <p>
                    Para confirmar seu endereço de e-mail, utilize o token abaixo
                    no endpoint POST /api/auth/confirmar-email.
                </p>
                <div style="padding: 16px; background: #f3f4f6; border-radius: 8px; overflow-wrap: anywhere;">
                    <strong>Token de confirmação:</strong>
                    <pre style="white-space: pre-wrap; overflow-wrap: anywhere;">{tokenSeguro}</pre>
                </div>
                """;
		}

		return $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Confirmação de cadastro - Sigera Associados</title>
            </head>
            <body style="font-family: Arial, sans-serif; color: #222; line-height: 1.6;">
                <main style="max-width: 600px; margin: 0 auto; padding: 24px;">
                    <h1 style="font-size: 24px;">Sigera Associados</h1>
                    <h2 style="font-size: 20px;">Confirmação de cadastro</h2>
                    <p>Sua conta foi cadastrada na Sigera Associados.</p>
                    {instrucoes}
                    <p style="font-size: 13px; color: #555;">
                        Por segurança, não compartilhe esta mensagem.
                        Se você não esperava este cadastro, ignore este e-mail
                        ou entre em contato com o administrador do sistema.
                    </p>
                </main>
            </body>
            </html>
            """;
	}
}
