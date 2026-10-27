namespace SigeraAssociados.Application.Financeiro;

/// <summary>
/// Forma de pagamento (T009FRPG). Na alteração, o código vem da rota.
/// </summary>
public sealed class FormaPagamentoDados
{
	/// <summary>
	/// Código (TP_FMA_PGT, 1 a 99). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? Codigo { get; init; }

	/// <summary>Descrição (DE_FMA_PGT, até 100). Obrigatória.</summary>
	/// <example>DINHEIRO</example>
	public string? Descricao { get; init; }

	/// <summary>
	/// Gera saldo (ID_GER_SLD): true grava 1, false grava 0. No crédito do
	/// associado, a forma que gera saldo deixa o saldo igual ao valor; a que
	/// não gera deixa o saldo zerado. Omitido: false.
	/// </summary>
	public bool? GeraSaldo { get; init; }
}

public static class ValidacaoFormaPagamento
{
	/// <summary>
	/// TP_FMA_PGT aceita até 999, mas T009EXTRA.TP_PGT é numeric(2,0): acima de 99
	/// a forma não poderia ser usada no crédito do associado.
	/// </summary>
	public const decimal CodigoMaximo = 99;

	/// <summary>Valor de ID_GER_SLD da forma que gera saldo.</summary>
	public const decimal GeraSaldo = 1;

	public const decimal NaoGeraSaldo = 0;

	public static string? Validar(FormaPagamentoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados da forma de pagamento.";
		}

		return (inclusao ? RegrasCampos.Inteiro(dados.Codigo, 1, CodigoMaximo, "codigo") : null)
			?? RegrasCampos.Obrigatorio(dados.Descricao, "descricao")
			?? RegrasCampos.Tamanho(dados.Descricao, 100, "descricao");
	}
}
