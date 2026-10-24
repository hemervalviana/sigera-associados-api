using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Autenticação simulada utilizada exclusivamente nos testes de integração.
/// Não deve ser registrada na aplicação de produção.
///
/// Por padrão autentica "usuario-teste" no grupo Administradores.
/// Cabeçalhos opcionais para simular outros perfis:
/// - X-Teste-Grupos: lista de grupos separados por vírgula (ex.: "Gestores");
/// - X-Teste-Anonimo: "true" para simular requisição sem token.
/// </summary>
public sealed class TesteAutenticacaoHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "TesteSigera";
    public const string CabecalhoGrupos = "X-Teste-Grupos";
    public const string CabecalhoAnonimo = "X-Teste-Anonimo";
    public const string UsuarioId = "usuario-teste";

    public TesteAutenticacaoHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.Equals(
                Request.Headers[CabecalhoAnonimo].ToString(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var grupos = Request.Headers[CabecalhoGrupos].ToString();

        var nomesGrupos = string.IsNullOrWhiteSpace(grupos)
            ? new[] { "Administradores" }
            : grupos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var reivindicacoes = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UsuarioId),
            new(ClaimTypes.Name, "Usuario de Teste"),
            new(ClaimTypes.Email, "teste@sigera.local")
        };

        reivindicacoes.AddRange(nomesGrupos.Select(g => new Claim(ClaimTypes.Role, g)));

        var identidade = new ClaimsIdentity(reivindicacoes, Esquema);
        var principal = new ClaimsPrincipal(identidade);
        var ticket = new AuthenticationTicket(principal, Esquema);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
