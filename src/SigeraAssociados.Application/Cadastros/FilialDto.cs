namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Filial cadastrada em T000PARA no sistema.
/// </summary>
public sealed class FilialDto
{
	public decimal CdFil { get; init; }

	public string Nome { get; init; } = string.Empty;

	public string? NomeCurto { get; init; }

	public string? Municipio { get; init; }

	public string? Uf { get; init; }
}
