namespace SigeraAssociados.Api.Services;

/// <summary>
/// Dados do token de acesso da requisição atual, guardados em
/// HttpContext.Items na validação do JWT (usados no logout).
/// </summary>
public static class SessaoAtual
{
	public const string ChaveTokenId = "sigera:token-id";
	public const string ChaveExpiracao = "sigera:token-expira-em";

	public static string? TokenId(HttpContext contexto) =>
		contexto.Items.TryGetValue(ChaveTokenId, out var valor) ? valor as string : null;

	public static DateTimeOffset? Expiracao(HttpContext contexto) =>
		contexto.Items.TryGetValue(ChaveExpiracao, out var valor) ? valor as DateTimeOffset? : null;
}
