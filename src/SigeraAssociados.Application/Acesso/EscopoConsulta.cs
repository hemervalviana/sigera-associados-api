namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Conjunto de escopos que limita as consultas do usuário atual
/// no sistema selecionado. Os escopos se somam (regra "OU").
/// </summary>
public sealed class EscopoConsulta
{
	private EscopoConsulta(
		bool acessoTotal,
		IReadOnlyList<EscopoAcesso> escopos)
	{
		AcessoTotal = acessoTotal;
		Escopos = escopos;
	}

	/// <summary>
	/// Indica que nenhum filtro de escopo deve ser aplicado.
	/// </summary>
	public bool AcessoTotal { get; }

	public IReadOnlyList<EscopoAcesso> Escopos { get; }

	/// <summary>
	/// Indica que o usuário não pode ver nenhum registro.
	/// </summary>
	public bool SemAcesso => !AcessoTotal && Escopos.Count == 0;

	public static EscopoConsulta Total { get; } =
		new(true, Array.Empty<EscopoAcesso>());

	public static EscopoConsulta Nenhum { get; } =
		new(false, Array.Empty<EscopoAcesso>());

	/// <summary>
	/// Cria o escopo a partir das linhas de acesso do usuário.
	/// Um escopo sem empresa e sem corretor equivale a acesso total.
	/// </summary>
	public static EscopoConsulta Criar(IEnumerable<EscopoAcesso> escopos)
	{
		ArgumentNullException.ThrowIfNull(escopos);

		var lista = escopos.Distinct().ToArray();

		if (lista.Any(e => e.CdEmp is null && e.SqPesCorretor is null))
		{
			return Total;
		}

		return lista.Length == 0
			? Nenhum
			: new EscopoConsulta(false, lista);
	}
}
