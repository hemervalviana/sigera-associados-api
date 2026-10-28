using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Contratos;

/// <summary>Categoria do contrato (T009CAT).</summary>
public sealed class CategoriaContratoDados
{
	/// <summary>Código (CD_CAT, 1 a 999). Na inclusão, omitir para usar MAX + 1; na alteração vem da rota.</summary>
	public decimal? CdCategoria { get; init; }

	/// <summary>Nome (NM_CAT, até 35). Obrigatório.</summary>
	public string? Descricao { get; init; }
}

/// <summary>Regras de formato da categoria (sem acesso ao banco).</summary>
public static class ValidacaoCategoriaContrato
{
	public const decimal CodigoMaximo = 999;

	public static string? Validar(CategoriaContratoDados? d, bool inclusao)
	{
		if (d is null)
		{
			return "Informe os dados da categoria.";
		}

		return (inclusao ? RegrasCampos.Inteiro(d.CdCategoria, 1, CodigoMaximo, "cdCategoria") : null)
			?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
			?? RegrasCampos.Tamanho(d.Descricao?.Trim(), 35, "descricao");
	}
}
