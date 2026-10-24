namespace SigeraAssociados.Application.Interfaces.Acesso;

/// <summary>
/// Validação das sessões a cada requisição autenticada.
///
/// Um token de acesso só vale se:
/// - o usuário ainda existe e não está bloqueado;
/// - o carimbo de segurança gravado no token é o atual do usuário
///   (muda ao bloquear, trocar de grupo, redefinir senha ou sair de todos
///   os dispositivos);
/// - o token não foi encerrado por logout.
/// </summary>
public interface ISessoesService
{
	Task<bool> SessaoValidaAsync(
		string usuarioId,
		string? carimboSeguranca,
		string? tokenId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Encerra um token de acesso específico (logout) até a sua expiração.
	/// </summary>
	void EncerrarTokenAcesso(string tokenId, DateTimeOffset expiraEm);

	/// <summary>
	/// Descarta os dados em cache do usuário, para que a próxima
	/// requisição consulte o banco (após bloqueio, troca de grupo etc.).
	/// </summary>
	void InvalidarCache(string usuarioId);
}
