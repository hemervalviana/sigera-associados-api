using SigeraAssociados.Application.Acesso;

namespace SigeraAssociados.Application.Interfaces.Acesso;

/// <summary>
/// Tokens de renovação (refresh tokens): permitem obter um novo token de
/// acesso sem informar a senha. São de uso único: cada renovação revoga o
/// token usado e emite outro. O banco guarda somente o hash do token.
/// </summary>
public interface ITokensRenovacaoService
{
	/// <summary>
	/// Emite um novo token de renovação para o usuário (login).
	/// </summary>
	Task<TokenRenovacaoEmitido> EmitirAsync(
		string usuarioId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Troca um token de renovação válido por um novo.
	/// Reuso de token já trocado revoga todas as sessões do usuário.
	/// </summary>
	Task<ResultadoRenovacao> RenovarAsync(
		string token,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Revoga um token de renovação do usuário (logout).
	/// </summary>
	Task<bool> RevogarAsync(
		string token,
		string usuarioId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Revoga todos os tokens de renovação ativos do usuário.
	/// </summary>
	Task<int> RevogarTodosAsync(
		string usuarioId,
		string motivo,
		CancellationToken cancellationToken = default);
}
