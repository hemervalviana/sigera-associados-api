using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Application.Interfaces.Usuarios;

/// <summary>
/// Consulta e manutenção das contas de usuário.
///
/// Regras aplicadas a todas as alterações:
/// - ninguém altera, bloqueia ou exclui a própria conta;
/// - somente administradores alteram contas de administradores;
/// - o grupo Administradores não é concedido por estas operações.
/// </summary>
public interface IGestaoUsuariosService
{
	/// <summary>
	/// Lista usuários com filtro por e-mail e grupo, ordenados por e-mail.
	/// </summary>
	Task<ConsultaUsuariosResultado> ListarAsync(
		ConsultaUsuariosRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Obtém um usuário. Retorna null quando não existe.
	/// </summary>
	Task<UsuarioResumoDto?> ObterAsync(
		string usuarioId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Substitui o grupo funcional do usuário pelo grupo informado.
	/// </summary>
	Task<ResultadoGestaoUsuario> AlterarGrupoAsync(
		string usuarioId,
		string grupo,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Bloqueia o login do usuário por tempo indeterminado.
	/// Tokens já emitidos continuam válidos até expirar.
	/// </summary>
	Task<ResultadoGestaoUsuario> BloquearAsync(
		string usuarioId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Remove o bloqueio e zera as tentativas de login com falha.
	/// </summary>
	Task<ResultadoGestaoUsuario> DesbloquearAsync(
		string usuarioId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclui a conta, seus grupos e seus acessos.
	/// </summary>
	Task<ResultadoGestaoUsuario> ExcluirAsync(
		string usuarioId,
		CancellationToken cancellationToken = default);
}
