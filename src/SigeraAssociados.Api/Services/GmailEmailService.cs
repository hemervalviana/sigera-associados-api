using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SigeraAssociados.Api.Services;

/// <summary>
/// Envia e-mails utilizando o servidor SMTP do Gmail.
/// As credenciais são obtidas da configuração segura da aplicação.
/// </summary>
public sealed class GmailEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailEmailService> _logger;

    public GmailEmailService(
        IConfiguration configuration,
        ILogger<GmailEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Envia uma mensagem de e-mail utilizando SMTP com STARTTLS.
    /// </summary>
    public async Task EnviarAsync(
        string destinatario,
        string assunto,
        string corpoHtml,
        CancellationToken cancellationToken = default)
    {
        // Obtém as configurações do Gmail armazenadas nos User Secrets
        // ou fornecidas por outro provedor seguro de configuração.
        var servidor = _configuration["Email:Gmail:Host"];
        var porta = _configuration.GetValue<int?>("Email:Gmail:Port");
        var usuario = _configuration["Email:Gmail:User"];
        var senha = _configuration["Email:Gmail:Password"];

        // Impede tentativas de envio com configuração incompleta.
        if (string.IsNullOrWhiteSpace(servidor) ||
            porta is null or < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(usuario) ||
            string.IsNullOrWhiteSpace(senha))
        {
            throw new InvalidOperationException(
                "O serviço de e-mail não está configurado corretamente.");
        }

        // Monta a mensagem com remetente, destinatário, assunto e HTML.
        var mensagem = new MimeMessage();

        mensagem.From.Add(
            new MailboxAddress("Sigera Associados", usuario));

        mensagem.To.Add(
            MailboxAddress.Parse(destinatario));

        mensagem.Subject = assunto;

        mensagem.Body = new BodyBuilder
        {
            HtmlBody = corpoHtml
        }.ToMessageBody();

        // Abre uma conexão SMTP com STARTTLS e autentica no Gmail.
        using var cliente = new SmtpClient();

        try
        {
            await cliente.ConnectAsync(
                servidor,
                porta.Value,
                SecureSocketOptions.StartTls,
                cancellationToken);

            await cliente.AuthenticateAsync(
                usuario,
                senha,
                cancellationToken);

            await cliente.SendAsync(
                mensagem,
                cancellationToken);

            _logger.LogInformation(
                "E-mail enviado com sucesso para o destinatário informado.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Registra o erro técnico sem incluir credenciais ou tokens.
            _logger.LogError(
                "Falha no envio de e-mail. Tipo de erro: {TipoErro}.",
                ex.GetType().Name);

            throw new InvalidOperationException(
                "Não foi possível enviar o e-mail. Verifique a configuração do serviço de e-mail.",
                ex);
        }
        finally
        {
            // Encerra a conexão SMTP quando ela estiver aberta.
            if (cliente.IsConnected)
            {
                try
                {
                    await cliente.DisconnectAsync(
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Não foi possível encerrar a conexão SMTP normalmente. Tipo de erro: {TipoErro}.",
                        ex.GetType().Name);
                }
            }
        }
    }
}