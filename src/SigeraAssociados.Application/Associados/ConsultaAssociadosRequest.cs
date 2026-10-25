namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Define os filtros, a paginação e a ordenação
/// para consultar associados.
/// </summary>
public sealed class ConsultaAssociadosRequest
{
	public decimal? CdEmp { get; init; }

	/// <summary>
	/// Começo do nome do associado (ex.: "MARIA" encontra "MARIA DA SILVA",
	/// mas não "ANA MARIA").
	/// </summary>
	public string? Nome { get; init; }

	public decimal? CdSitCli { get; init; }

	/// <summary>
	/// CPF completo, com ou sem pontuação. Busca exata.
	/// Depois da validação no serviço, contém só os 11 números.
	/// </summary>
	public string? Cpf { get; init; }

	public int Pagina { get; init; } = 1;

	public int TamanhoPagina { get; init; } = 20;

	public string OrdenarPor { get; init; } = "nome";

	public string Direcao { get; init; } = "asc";
}