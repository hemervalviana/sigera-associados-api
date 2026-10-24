using SigeraAssociados.Application.Acesso;

namespace SigeraAssociados.Application.Interfaces.Acesso;

/// <summary>
/// Operações administrativas sobre os escopos de acesso dos usuários.
/// </summary>
public interface IGestaoAcessosService
{
	/// <summary>
	/// Lista os acessos do usuário. Retorna null quando o usuário não existe.
	/// </summary>
	Task<IReadOnlyList<UsuarioAcessoDto>?> ListarAsync(
		string usuarioId,
		CancellationToken cancellationToken = default);

	Task<ResultadoGestaoAcesso> ConcederAsync(
		string usuarioId,
		NovoAcessoDto acesso,
		CancellationToken cancellationToken = default);

	Task<ResultadoGestaoAcesso> RevogarAsync(
		string usuarioId,
		int acessoId,
		CancellationToken cancellationToken = default);
}
