using Microsoft.AspNetCore.Identity;

namespace SigeraAssociados.Infrastructure.Persistence.Identity;

/// <summary>
/// Token de renovação (refresh token) de um usuário.
/// Guarda somente o hash SHA-256 do token, nunca o valor original.
/// </summary>
public sealed class TokenRenovacao
{
	public long Id { get; set; }

	public string UsuarioId { get; set; } = string.Empty;

	/// <summary>
	/// SHA-256 do token, em hexadecimal (64 caracteres).
	/// </summary>
	public string TokenHash { get; set; } = string.Empty;

	public DateTime CriadoEmUtc { get; set; }

	public DateTime ExpiraEmUtc { get; set; }

	/// <summary>
	/// Preenchido quando o token é trocado, encerrado por logout
	/// ou revogado por segurança.
	/// </summary>
	public DateTime? RevogadoEmUtc { get; set; }

	/// <summary>
	/// Motivo da revogação: renovado, logout, bloqueio, senha_redefinida,
	/// reuso_detectado, logout_todos.
	/// </summary>
	public string? MotivoRevogacao { get; set; }

	/// <summary>
	/// Hash do token que substituiu este na renovação.
	/// </summary>
	public string? SubstituidoPorHash { get; set; }

	public IdentityUser? Usuario { get; set; }
}
