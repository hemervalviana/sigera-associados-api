namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Representa uma linha de acesso cadastrada para um usuário.
/// </summary>
public sealed class UsuarioAcessoDto
{
	public int Id { get; init; }

	public string UsuarioId { get; init; } = string.Empty;

	public int Sistema { get; init; }

	public string NomeSistema { get; init; } = string.Empty;

	public decimal? CdFil { get; init; }

	public decimal? SqPesCorretor { get; init; }

	public DateTime CriadoEmUtc { get; init; }
}
