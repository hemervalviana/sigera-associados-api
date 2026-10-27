namespace SigeraAssociados.Application.Financeiro;

/// <summary>
/// Tipo de governo do convênio (T009CONV.CT_TP_GOV): valores fixos do
/// legado, sem tabela no banco. Para incluir um tipo novo, acrescente aqui.
/// </summary>
public static class TipoGovernoConvenio
{
	public static readonly IReadOnlyDictionary<decimal, string> Tipos = new Dictionary<decimal, string>
	{
		[1] = "GOVERNO DO ACRE",
		[2] = "GOVERNO DO AMAPÁ",
		[3] = "GOVERNO DO PARÁ",
		[4] = "GOVERNO DO PIAUÍ",
		[5] = "GOVERNO DE MANAUS",
		[6] = "GOVERNO DE SERGIPE",
		[7] = "GOVERNO DE NATAL",
		[8] = "GOVERNO DE RORAIMA",
		[9] = "SERGIPE PREVIDÊNCIA",
		[10] = "GOVERNO DA PARAÍBA",
		[11] = "PREFEITURA DE ARACAJU",
		[12] = "BANCO DO BRASIL",
		[13] = "PREFEITURA DE SOCORRO",
		[14] = "PREFEITURA DE SOBRAL",
		[15] = "PREFEITURA DE AQUIRAZ",
		[16] = "CAMPINA GRANDE",
		[17] = "PREFEITURA DE RIO BRANCO",
		[18] = "PREFEITURA DE ANANINDEUA",
		[19] = "PREFEITURA DE BELÉM",
		[20] = "IGEPREV PARÁ"
	};

	/// <summary>Descrição do tipo, ou null (vazio ou código desconhecido).</summary>
	public static string? Descricao(decimal? codigo) =>
		codigo is decimal c && Tipos.TryGetValue(c, out var descricao) ? descricao : null;

	/// <summary>Vazio é aceito; informado, precisa estar na lista.</summary>
	public static bool Valido(decimal? codigo) => codigo is null || Tipos.ContainsKey(codigo.Value);

	/// <summary>Lista para a tela, ordenada pelo código.</summary>
	public static IReadOnlyList<TipoGovernoDto> Listar() =>
		Tipos.OrderBy(t => t.Key).Select(t => new TipoGovernoDto { Codigo = t.Key, Descricao = t.Value }).ToList();
}

public sealed class TipoGovernoDto
{
	public decimal Codigo { get; init; }

	public string Descricao { get; init; } = string.Empty;
}
