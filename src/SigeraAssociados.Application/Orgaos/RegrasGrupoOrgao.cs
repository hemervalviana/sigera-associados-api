namespace SigeraAssociados.Application.Orgaos;

/// <summary>
/// Valores fixos do grupo de órgãos (T009GRUP), definidos pelo sistema
/// legado, sem tabela no banco.
/// - TP_GP (tipo): F = DIA FIXO, U = DIA ÚTIL.
/// - ID_ORD (ordem): A = ASCENDENTE, D = DESCENDENTE.
/// - NR_DD (número de dias): 1 a 31.
/// </summary>
public static class RegrasGrupoOrgao
{
	public const string TipoDiaFixo = "F";
	public const string TipoDiaUtil = "U";

	public const string OrdemAscendente = "A";
	public const string OrdemDescendente = "D";

	public const int NumeroDiasMinimo = 1;
	public const int NumeroDiasMaximo = 31;

	public static string? DescricaoTipo(string? tipo) => Normalizar(tipo) switch
	{
		TipoDiaFixo => "DIA FIXO",
		TipoDiaUtil => "DIA ÚTIL",
		_ => null
	};

	public static string? DescricaoOrdem(string? ordem) => Normalizar(ordem) switch
	{
		OrdemAscendente => "ASCENDENTE",
		OrdemDescendente => "DESCENDENTE",
		_ => null
	};

	public static bool TipoValido(string? tipo) => DescricaoTipo(tipo) is not null;

	public static bool OrdemValida(string? ordem) => DescricaoOrdem(ordem) is not null;

	public static string? Normalizar(string? valor) =>
		string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();
}
