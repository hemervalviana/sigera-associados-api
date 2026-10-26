using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Usuarios;

/// <summary>
/// Grupo funcional em que o usuário será incluído.
/// </summary>
public sealed class AdicionarGrupoRequest
{
	/// <summary>
	/// Nome de um dos grupos funcionais (veja GET /api/administracao/grupos).
	/// Não diferencia maiúsculas. Administradores não é aceito.
	/// </summary>
	/// <example>Financeiro</example>
	[Required(ErrorMessage = "O grupo é obrigatório.")]
	[StringLength(100, ErrorMessage = "O grupo não pode ultrapassar 100 caracteres.")]
	public string Grupo { get; init; } = string.Empty;
}
