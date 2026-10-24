using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Opções das sessões (seção Jwt do appsettings).
/// </summary>
public sealed class OpcoesSessao
{
	public const string SectionName = "Jwt";

	/// <summary>
	/// Validade do token de renovação, em dias (padrão 7).
	/// </summary>
	[Range(1, 90, ErrorMessage = "Jwt:RefreshTokenDias deve estar entre 1 e 90.")]
	public int RefreshTokenDias { get; set; } = 7;
}
