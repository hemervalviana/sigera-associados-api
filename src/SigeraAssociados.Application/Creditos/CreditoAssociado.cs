using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Creditos;

/// <summary>
/// Dados de inclusão e alteração de um crédito do associado (T009EXTRA).
/// Na alteração, a sequência vem da rota. O saldo (VR_SLD) não é enviado:
/// a API calcula pela forma de pagamento, como o legado.
/// </summary>
public sealed class CreditoDados
{
	/// <summary>Data do crédito (DT_PGT). Omitida na inclusão: hoje.</summary>
	public DateTime? DataCredito { get; init; }

	/// <summary>
	/// Valor do crédito (VR_PGT), com até 2 casas. Não pode ser negativo.
	/// Omitido: 0 (como o legado faz com o campo vazio).
	/// </summary>
	/// <example>150.00</example>
	public decimal? Valor { get; init; }

	/// <summary>
	/// Forma de pagamento (TP_PGT), de GET /api/cadastros/formas-pagamento.
	/// Obrigatória. Define o saldo: forma que gera saldo deixa saldo = valor;
	/// a que não gera deixa saldo 0. O código 0 (sem forma) deixa saldo = valor.
	/// </summary>
	public decimal? CdFormaPagamento { get; init; }

	/// <summary>
	/// Tipo de baixa (ID_PGT_AUT): 1 = AUTOMÁTICA, 2 = MANUAL, 4 = CRÉDITO.
	/// Omitido na inclusão: 4 (CRÉDITO).
	/// </summary>
	public decimal? TipoBaixa { get; init; }

	/// <summary>Número do cheque (NR_CHQ, até 10 dígitos).</summary>
	public decimal? NumeroCheque { get; init; }

	/// <summary>Observação (DE_OBS, até 100).</summary>
	public string? Observacao { get; init; }

	/// <summary>Filial (CD_FIL). Obrigatória quando há contrato ou averbação.</summary>
	public decimal? CdFil { get; init; }

	/// <summary>Contrato (SQ_CTR) do associado, na filial informada.</summary>
	public decimal? SqContrato { get; init; }

	/// <summary>Número da parcela (NR_PCL, até 5 dígitos).</summary>
	public decimal? NumeroParcela { get; init; }

	/// <summary>Convênio (CD_CVN), de GET /api/convenios.</summary>
	public decimal? CdConvenio { get; init; }

	/// <summary>
	/// Código da averbação (CD_AVB). Exige filial e convênio; a combinação
	/// é conferida em T009RBSR.
	/// </summary>
	public decimal? CdAverbacao { get; init; }

	/// <summary>Matrícula (MT_PES, até 20).</summary>
	public string? Matricula { get; init; }

	/// <summary>Dígito da matrícula (DV_MT, até 5).</summary>
	public string? DigitoMatricula { get; init; }

	/// <summary>Produto (CD_PRD), de GET /api/cadastros/produtos.</summary>
	public decimal? CdProduto { get; init; }
}

/// <summary>
/// Crédito do associado com as descrições.
/// </summary>
public sealed class CreditoDto
{
	/// <summary>Sequência do crédito do associado (SQ_PGT).</summary>
	public decimal SqCredito { get; init; }

	public DateTime? DataCredito { get; init; }

	public decimal Valor { get; init; }

	/// <summary>Saldo do crédito (VR_SLD), calculado pela API.</summary>
	public decimal Saldo { get; init; }

	public decimal CdFormaPagamento { get; init; }

	public string? FormaPagamento { get; init; }

	public decimal TipoBaixa { get; init; }

	/// <summary>AUTOMÁTICA, MANUAL ou CRÉDITO.</summary>
	public string? DescricaoTipoBaixa { get; init; }

	public decimal? NumeroCheque { get; init; }

	public string? Observacao { get; init; }

	public decimal? CdFil { get; init; }

	public decimal? SqContrato { get; init; }

	public decimal? NumeroParcela { get; init; }

	public decimal? CdConvenio { get; init; }

	public string? Convenio { get; init; }

	public decimal? CdAverbacao { get; init; }

	public string? Matricula { get; init; }

	public string? DigitoMatricula { get; init; }

	public decimal? CdProduto { get; init; }

	public string? Produto { get; init; }

	/// <summary>
	/// True quando valor e saldo são iguais: a regra do legado só deixa
	/// excluir nesse caso.
	/// </summary>
	public bool PodeExcluir { get; init; }
}

/// <summary>
/// Regras do crédito do associado copiadas do legado (sem acesso ao banco).
/// </summary>
public static class RegrasCredito
{
	/// <summary>Valores de T009EXTRA.ID_PGT_AUT.</summary>
	public const decimal BaixaAutomatica = 1;
	public const decimal BaixaManual = 2;
	public const decimal BaixaCredito = 4;

	/// <summary>SQ_PGT é numeric(8,0).</summary>
	public const decimal SequenciaMaxima = 99_999_999;

	/// <summary>VR_PGT é numeric(11,2).</summary>
	public const decimal ValorMaximo = 999_999_999.99m;

	// DT_PGT é datetime; a faixa aceita fica entre 1900 e 2079 (smalldatetime do legado).
	private static readonly DateTime DataMinima = new(1900, 1, 1);
	private static readonly DateTime DataMaxima = new(2079, 6, 6);

	public static string? DescricaoTipoBaixa(decimal? tipo) => tipo switch
	{
		BaixaAutomatica => "AUTOMÁTICA",
		BaixaManual => "MANUAL",
		BaixaCredito => "CRÉDITO",
		_ => null
	};

	/// <summary>
	/// Saldo do crédito, como o itemchanged do legado: forma 0 (sem forma)
	/// deixa saldo = valor; forma que gera saldo (ID_GER_SLD = 1) também;
	/// qualquer outra forma deixa o saldo zerado.
	/// </summary>
	public static decimal CalcularSaldo(decimal valor, decimal formaPagamento, decimal? idGeraSaldo)
	{
		if (valor <= 0)
		{
			return 0;
		}

		if (formaPagamento <= 0)
		{
			return valor;
		}

		return idGeraSaldo == ValidacaoFormaPagamento.GeraSaldo ? valor : 0;
	}

	/// <summary>
	/// Regra de exclusão do legado: só exclui quando o valor do pagamento é
	/// igual ao saldo (vazio conta como 0).
	/// </summary>
	public static bool PodeExcluir(decimal? valor, decimal? saldo) => (valor ?? 0) == (saldo ?? 0);

	public static string? Validar(CreditoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do crédito.";
		}

		if (dados.Valor is decimal valor)
		{
			if (valor < 0)
			{
				return "Valor inválido do pagamento: não pode ser negativo.";
			}

			if (valor > ValorMaximo || decimal.Round(valor, 2) != valor)
			{
				return "O campo valor aceita até 999.999.999,99, com no máximo 2 casas decimais.";
			}
		}

		if (dados.CdFormaPagamento is null)
		{
			return "A forma de pagamento (cdFormaPagamento) é obrigatória.";
		}

		if (dados.TipoBaixa is not null && DescricaoTipoBaixa(dados.TipoBaixa) is null)
		{
			return "O campo tipoBaixa deve ser 1 (AUTOMÁTICA), 2 (MANUAL) ou 4 (CRÉDITO).";
		}

		if (dados.DataCredito is DateTime data && (data < DataMinima || data > DataMaxima))
		{
			return "O campo dataCredito deve estar entre 01/01/1900 e 06/06/2079.";
		}

		var erro = RegrasCampos.Inteiro(dados.CdFormaPagamento, 0, 99, "cdFormaPagamento")
			?? RegrasCampos.Inteiro(dados.NumeroCheque, 0, 9_999_999_999m, "numeroCheque")
			?? RegrasCampos.Tamanho(dados.Observacao, 100, "observacao")
			?? RegrasCampos.Inteiro(dados.CdFil, 1, 99, "cdFil")
			?? RegrasCampos.Inteiro(dados.SqContrato, 1, 99_999_999, "sqContrato")
			?? RegrasCampos.Inteiro(dados.NumeroParcela, 0, 99_999, "numeroParcela")
			?? RegrasCampos.Inteiro(dados.CdConvenio, 1, 999, "cdConvenio")
			?? RegrasCampos.Inteiro(dados.CdAverbacao, 1, 99_999, "cdAverbacao")
			?? RegrasCampos.Tamanho(dados.Matricula, 20, "matricula")
			?? RegrasCampos.Tamanho(dados.DigitoMatricula, 5, "digitoMatricula")
			?? RegrasCampos.Inteiro(dados.CdProduto, 1, 999, "cdProduto");

		if (erro is not null)
		{
			return erro;
		}

		if (dados.SqContrato is not null && dados.CdFil is null)
		{
			return "Informe a filial (cdFil) do contrato.";
		}

		if (dados.CdAverbacao is not null && (dados.CdFil is null || dados.CdConvenio is null))
		{
			return "Para informar a averbação, informe também a filial (cdFil) e o convênio (cdConvenio).";
		}

		return null;
	}
}
