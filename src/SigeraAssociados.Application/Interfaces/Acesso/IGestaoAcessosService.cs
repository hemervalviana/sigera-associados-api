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

	/// <summary>
	/// Concede um escopo. Com sistema 0 (todos), grava uma linha para cada
	/// sistema que o usuário ainda não tem; as linhas gravadas voltam em Acessos.
	/// </summary>
	Task<ResultadoGestaoAcesso> ConcederAsync(
		string usuarioId,
		NovoAcessoDto acesso,
		CancellationToken cancellationToken = default);

	Task<ResultadoGestaoAcesso> RevogarAsync(
		string usuarioId,
		int acessoId,
		CancellationToken cancellationToken = default);
}
