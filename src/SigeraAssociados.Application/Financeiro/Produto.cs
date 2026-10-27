namespace SigeraAssociados.Application.Financeiro;

/// <summary>
/// Produto (T009PRDT). Na alteração, o código vem da rota.
/// </summary>
public sealed class ProdutoDados
{
	/// <summary>
	/// Código (CD_PRD, 1 a 99). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? Codigo { get; init; }

	/// <summary>Descrição (DE_PRD, até 100). Obrigatória.</summary>
	public string? Descricao { get; init; }
}

public static class ValidacaoProduto
{
	public const decimal CodigoMaximo = 99;

	public static string? Validar(ProdutoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do produto.";
		}

		return (inclusao ? RegrasCampos.Inteiro(dados.Codigo, 1, CodigoMaximo, "codigo") : null)
			?? RegrasCampos.Obrigatorio(dados.Descricao, "descricao")
			?? RegrasCampos.Tamanho(dados.Descricao, 100, "descricao");
	}
}
