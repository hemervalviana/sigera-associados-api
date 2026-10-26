namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Filtros da pesquisa de corretores de um sistema.
/// </summary>
public sealed class ConsultaCorretoresRequest
{
	/// <summary>
	/// Filial. Quando informada, traz só os corretores dessa filial.
	/// </summary>
	public decimal? CdFil { get; init; }

	/// <summary>
	/// Começo do nome ou o código do corretor.
	/// </summary>
	public string? Busca { get; init; }

	public int Pagina { get; init; } = 1;

	public int TamanhoPagina { get; init; } = 50;
}
