using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Token de renovação recebido no login (ou na última renovação).
/// </summary>
public sealed class RenovarTokenRequest
{
	[Required(ErrorMessage = "O refreshToken é obrigatório.")]
	[StringLength(200, ErrorMessage = "O refreshToken é muito longo.")]
	public string RefreshToken { get; init; } = string.Empty;
}
