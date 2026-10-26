namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Município cadastrado em T000MUNI.
/// </summary>
public sealed class MunicipioDto
{
	public decimal CdMun { get; init; }

	public string Nome { get; init; } = string.Empty;

	public string Uf { get; init; } = string.Empty;
}
