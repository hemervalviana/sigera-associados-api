namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Página de corretores encontrados.
/// </summary>
public sealed class ConsultaCorretoresResultado
{
	public int Pagina { get; init; }

	public int TamanhoPagina { get; init; }

	public int TotalRegistros { get; init; }

	public int TotalPaginas { get; init; }

	public IReadOnlyList<CorretorDto> Corretores { get; init; }
		= Array.Empty<CorretorDto>();
}
