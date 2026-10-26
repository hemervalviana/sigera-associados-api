namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Página de municípios encontrados.
/// </summary>
public sealed class ConsultaMunicipiosResultado
{
	public int Pagina { get; init; }

	public int TamanhoPagina { get; init; }

	public int TotalRegistros { get; init; }

	public int TotalPaginas { get; init; }

	public IReadOnlyList<MunicipioDto> Municipios { get; init; }
		= Array.Empty<MunicipioDto>();
}
