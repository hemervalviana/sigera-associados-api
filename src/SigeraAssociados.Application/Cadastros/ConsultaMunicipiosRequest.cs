namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Filtros da pesquisa de municípios.
/// </summary>
public sealed class ConsultaMunicipiosRequest
{
	/// <summary>
	/// UF com 2 letras (opcional).
	/// </summary>
	public string? Uf { get; init; }

	/// <summary>
	/// Começo do nome ou o código do município (opcional).
	/// </summary>
	public string? Busca { get; init; }

	public int Pagina { get; init; } = 1;

	public int TamanhoPagina { get; init; } = 50;
}
