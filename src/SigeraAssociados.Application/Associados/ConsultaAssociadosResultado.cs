namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Representa o resultado de uma consulta paginada de associados.
/// </summary>
public sealed class ConsultaAssociadosResultado
{
	public decimal? CdEmp { get; init; }

	public string? Nome { get; init; }

	public decimal? CdSitCli { get; init; }

	public int Pagina { get; init; }

	public int TamanhoPagina { get; init; }

	public int TotalRegistros { get; init; }

	public int TotalPaginas { get; init; }

	public string CampoOrdenacao { get; init; } = "nome";

	public string Direcao { get; init; } = "asc";

	public int Quantidade { get; init; }

	public IReadOnlyList<AssociadoDto> Associados { get; init; }
		= Array.Empty<AssociadoDto>();
}