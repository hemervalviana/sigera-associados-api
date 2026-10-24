namespace SigeraAssociados.Application.Usuarios;

/// <summary>
/// Dados de um usuário exibidos na gestão de usuários.
/// Não contém senha, tokens nem dados de segurança.
/// </summary>
public sealed class UsuarioResumoDto
{
	public string Id { get; init; } = string.Empty;

	public string Email { get; init; } = string.Empty;

	public bool EmailConfirmado { get; init; }

	/// <summary>
	/// Indica se a conta está bloqueada no momento.
	/// </summary>
	public bool Bloqueado { get; init; }

	/// <summary>
	/// Fim do bloqueio, quando houver. Bloqueio por tempo indeterminado
	/// usa a data máxima.
	/// </summary>
	public DateTimeOffset? BloqueadoAte { get; init; }

	public IReadOnlyList<string> Grupos { get; init; } = Array.Empty<string>();
}
