using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Usuarios;

/// <summary>
/// Novo grupo funcional do usuário.
/// </summary>
public sealed class AlterarGrupoRequest
{
	[Required(ErrorMessage = "O grupo é obrigatório.")]
	[StringLength(100, ErrorMessage = "O grupo não pode ultrapassar 100 caracteres.")]
	public string Grupo { get; init; } = string.Empty;
}
