using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Dados do logout.
/// </summary>
public sealed class LogoutRequest
{
	/// <summary>
	/// Token de renovação desta sessão, para que não possa mais ser usado.
	/// </summary>
	[StringLength(200, ErrorMessage = "O refreshToken é muito longo.")]
	public string? RefreshToken { get; init; }

	/// <summary>
	/// true encerra as sessões em todos os dispositivos.
	/// </summary>
	public bool TodosOsDispositivos { get; init; }
}
