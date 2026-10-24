namespace SigeraAssociados.Application.Usuarios;

/// <summary>
/// Situação final de uma operação sobre um usuário.
/// </summary>
public enum StatusGestaoUsuario
{
	Sucesso,
	UsuarioNaoEncontrado,
	Invalido,

	/// <summary>
	/// O executor não pode realizar a operação sobre esse usuário
	/// (por exemplo, sobre a própria conta ou, sendo gestor,
	/// sobre um administrador).
	/// </summary>
	Proibido,

	Falha
}

public sealed record ResultadoGestaoUsuario(
	StatusGestaoUsuario Status,
	string? Mensagem = null,
	UsuarioResumoDto? Usuario = null);
