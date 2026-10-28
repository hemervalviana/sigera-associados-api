using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Contratos;

/// <summary>
/// Regras do contrato copiadas do legado (w_r_t009ctrt), sem acesso ao banco.
/// </summary>
public static class RegrasContrato
{
	/// <summary>Situações com regra própria (T009SCTR.CD_ST_CTR).</summary>
	public const decimal SituacaoEncerrado = 4;
	public const decimal SituacaoQuitado = 9;
	public const decimal SituacaoCancelado = 10;
	public const decimal SituacaoFinalizado = 12;
	public const decimal SituacaoQuitadoPorDentro = 13;

	/// <summary>
	/// Produto em que DT_ULT_DCT recebe o vencimento da última parcela
	/// (pfc_preupdate: "se auxílio financeiro", cd_prd = 2).
	/// </summary>
	public const decimal ProdutoAuxilioFinanceiro = 2;

	public const decimal SequenciaMaxima = 99_999_999;
	public const int ParcelasMaximo = 999;
	public const decimal NumeroParcelaMaximo = 99_999;

	private const decimal Valor11 = 999_999_999.99m;
	private const decimal Valor13 = 99_999_999_999.99m;
	private const decimal Valor7 = 99_999.99m;
	private const decimal IndiceMaximo = 9.9999m;

	/// <summary>
	/// Validação de campos (pfc_validation e itemchanged), sem consultar tabelas.
	/// </summary>
	public static string? Validar(ContratoDados? d, bool inclusao)
	{
		if (d is null)
		{
			return "Informe os dados do contrato.";
		}

		return Obrigatorios(d, inclusao)
			?? (inclusao ? PrimeiraParcela(d.DataPrimeiraParcela, d.DataContrato ?? DateTime.Today) : null)
			?? DatasDaSituacao(d.CdSituacao, d.DataQuitacao, d.DataFinalizacao, d.DataCancelamento)
			?? Valores(d)
			?? Codigos(d)
			?? RegrasCampos.Tamanho(d.Observacao, 2000, "observacao")
			?? RegrasCampos.Tamanho(d.ObservacaoSnt, 255, "observacaoSnt");
	}

	private static string? Obrigatorios(ContratoDados d, bool inclusao)
	{
		if (inclusao && d.CdFil is null) return "A filial (cdFil) é obrigatória.";
		if (d.SqPesCli is null or <= 0) return "O associado (sqPesCli) é obrigatório.";
		if (d.CdProduto is null or <= 0) return "O produto (cdProduto) é obrigatório.";
		if (d.CdConvenio is null or <= 0) return "O convênio (cdConvenio) é obrigatório.";
		if (d.CdFormaPagamento is null) return "A forma de pagamento (cdFormaPagamento) é obrigatória.";
		if (d.CdSituacao is null) return "A situação (cdSituacao) é obrigatória.";
		if (d.SqOrgao is null or <= 0) return "O órgão do associado (sqOrgao) é obrigatório.";
		if (d.SqContaAssociado is null or <= 0) return "A conta bancária do associado (sqContaAssociado) é obrigatória.";
		if (d.DataPrimeiraParcela is null) return "A data da 1ª parcela (dataPrimeiraParcela) é obrigatória.";
		if (d.ValorContrato is null or <= 0) return "O valor do contrato (valorContrato) é obrigatório e deve ser maior que zero.";
		if (inclusao && d.QuantidadeParcelas is null or <= 0) return "A quantidade de parcelas (quantidadeParcelas) é obrigatória.";
		if (d.CdCategoria is null or <= 0) return "A categoria (cdCategoria) é obrigatória.";
		return null;
	}

	/// <summary>A 1ª parcela vem depois da data do contrato.</summary>
	public static string? PrimeiraParcela(DateTime? primeiraParcela, DateTime dataContrato) =>
		primeiraParcela is DateTime p && p.Date <= dataContrato.Date
			? "A data da 1ª parcela não pode ser menor ou igual à data do contrato."
			: null;

	/// <summary>
	/// Cada data de encerramento anda junto com a sua situação:
	/// quitação com 9 ou 13, finalização com 12, cancelamento com 10.
	/// </summary>
	public static string? DatasDaSituacao(decimal? situacao, DateTime? quitacao, DateTime? finalizacao, DateTime? cancelamento)
	{
		var quitado = situacao is SituacaoQuitado or SituacaoQuitadoPorDentro;

		if (quitado && quitacao is null)
			return "Para a situação Quitado ou Quitado Pgto por Dentro é necessária a referência de quitação (dataQuitacao).";
		if (!quitado && quitacao is not null)
			return "A referência de quitação (dataQuitacao) só pode ser informada com a situação Quitado (9) ou Quitado Pgto por Dentro (13).";
		if (situacao == SituacaoFinalizado && finalizacao is null)
			return "Para a situação Finalizado é necessária a data de finalização (dataFinalizacao).";
		if (situacao != SituacaoFinalizado && finalizacao is not null)
			return "A data de finalização (dataFinalizacao) só pode ser informada com a situação Finalizado (12).";
		if (situacao == SituacaoCancelado && cancelamento is null)
			return "Para a situação Cancelado é necessária a data de cancelamento (dataCancelamento).";
		if (situacao != SituacaoCancelado && cancelamento is not null)
			return "A data de cancelamento (dataCancelamento) só pode ser informada com a situação Cancelado (10).";
		return null;
	}

	private static string? Valores(ContratoDados d) =>
		Monetario(d.ValorContrato, Valor11, "valorContrato")
		?? Monetario(d.ValorMensalidade, Valor7, "valorMensalidade")
		?? Monetario(d.ValorCoberturaPeculio, Valor11, "valorCoberturaPeculio")
		?? Monetario(d.ValorPagoFora, Valor11, "valorPagoFora")
		?? Monetario(d.ValorQuitacao, Valor13, "valorQuitacao")
		?? Monetario(d.ValorCompraDivida, Valor13, "valorCompraDivida")
		?? Monetario(d.ValorTarifaBancaria, 99.99m, "valorTarifaBancaria")
		?? Monetario(d.ValorDespesaAdministrativa, Valor7, "valorDespesaAdministrativa")
		?? Monetario(d.ValorIna, Valor11, "valorIna")
		?? Monetario(d.ValorComissao, Valor11, "valorComissao")
		?? Monetario(d.ValorDeposito, Valor11, "valorDeposito")
		?? (d.Indice is decimal i && (i < 0 || i > IndiceMaximo || decimal.Round(i, 4) != i)
			? "O índice deve estar entre 0 e 9,9999, com até 4 casas."
			: null);

	private static string? Codigos(ContratoDados d) =>
		RegrasCampos.Inteiro(d.CdFil, 0, 99, "cdFil")
		?? RegrasCampos.Inteiro(d.SqPesCli, 1, 9_999_999, "sqPesCli")
		?? RegrasCampos.Inteiro(d.CdProduto, 1, 99, "cdProduto")
		?? RegrasCampos.Inteiro(d.CdConvenio, 1, 999, "cdConvenio")
		?? RegrasCampos.Inteiro(d.CdFormaPagamento, 0, 999, "cdFormaPagamento")
		?? RegrasCampos.Inteiro(d.CdSituacao, 0, 99, "cdSituacao")
		?? RegrasCampos.Inteiro(d.SqOrgao, 1, 999, "sqOrgao")
		?? RegrasCampos.Inteiro(d.SqContaAssociado, 1, 99, "sqContaAssociado")
		?? RegrasCampos.Inteiro(d.CdMunicipio, 1, 9_999_999, "cdMunicipio")
		?? RegrasCampos.Inteiro(d.QuantidadeParcelas, 1, ParcelasMaximo, "quantidadeParcelas")
		?? RegrasCampos.Inteiro(d.CdCarencia, 0, 9, "cdCarencia")
		?? RegrasCampos.Inteiro(d.SqConsignante, 1, 9_999_999, "sqConsignante")
		?? RegrasCampos.Inteiro(d.NumeroProposta, 0, 999_999, "numeroProposta")
		?? RegrasCampos.Inteiro(d.TipoCompraDivida, 0, 99, "tipoCompraDivida")
		?? RegrasCampos.Inteiro(d.CdTipoContrato, 0, 99, "cdTipoContrato")
		?? RegrasCampos.Inteiro(d.SqPesCorretor, 1, 9_999_999, "sqPesCorretor")
		?? RegrasCampos.Inteiro(d.SqContaCorretor, 1, 99, "sqContaCorretor")
		?? RegrasCampos.Inteiro(d.CdCategoria, 1, 999, "cdCategoria")
		?? RegrasCampos.Inteiro(d.CdOrigemRecurso, 0, 99_999, "cdOrigemRecurso")
		?? RegrasCampos.Inteiro(d.SqProdutor, 1, 9_999_999, "sqProdutor")
		?? RegrasCampos.Inteiro(d.CdBanco, 0, 999, "cdBanco")
		?? RegrasCampos.Inteiro(d.CdEntFisi, 0, 9, "cdEntFisi");

	/// <summary>Valor de 0 até o máximo da coluna, com até 2 casas.</summary>
	public static string? Monetario(decimal? valor, decimal maximo, string campo) =>
		valor is decimal v && (v < 0 || v > maximo || decimal.Round(v, 2) != v)
			? $"O campo {campo} deve estar entre 0 e {maximo:N2}, com até 2 casas."
			: null;

	/// <summary>
	/// Índice e mensalidade (itemchanged de vr_ind e vr_mns): um completa o
	/// outro pelo valor do contrato. Índice vindo de T009INDI só é usado
	/// quando nenhum dos dois foi enviado (wf_buscar_indice).
	/// </summary>
	public static (decimal? Indice, decimal? Mensalidade) IndiceEMensalidade(
		decimal valorContrato,
		decimal? indice,
		decimal? mensalidade,
		decimal? indiceTabela)
	{
		indice ??= mensalidade is null ? indiceTabela : null;

		if (mensalidade is null && indice is decimal i)
		{
			mensalidade = decimal.Round(valorContrato * i, 2, MidpointRounding.AwayFromZero);
		}
		else if (indice is null && mensalidade is decimal m && valorContrato > 0)
		{
			var calculado = decimal.Round(m / valorContrato, 4, MidpointRounding.AwayFromZero);
			indice = calculado <= IndiceMaximo ? calculado : null;
		}

		return (indice, mensalidade);
	}

	/// <summary>
	/// Encerramento (itemchanged de cd_st_ctr): hoje quando a situação passa
	/// a 4 ou 9; limpa nas outras. Situação mantida: mantém a data.
	/// </summary>
	public static DateTime? DataEncerramento(decimal? situacaoAnterior, decimal? situacaoNova, DateTime? atual, DateTime hoje)
	{
		if (situacaoAnterior is not null && situacaoAnterior == situacaoNova)
		{
			return atual;
		}

		return situacaoNova is SituacaoEncerrado or SituacaoQuitado ? hoje.Date : null;
	}

	/// <summary>
	/// Parcelas da inclusão (wf_gerar_parcelas_02): quantidade de parcelas de
	/// mensalidade + cobertura do pecúlio, a 1ª na data informada e as demais
	/// mês a mês (dia 31 em mês curto vira o último dia do mês).
	/// </summary>
	public static IReadOnlyList<(decimal Numero, DateTime Vencimento, decimal Valor)> GerarParcelas(
		DateTime primeiraParcela,
		int quantidade,
		decimal mensalidade,
		decimal? coberturaPeculio)
	{
		var valor = mensalidade + (coberturaPeculio ?? 0);
		var parcelas = new List<(decimal, DateTime, decimal)>(quantidade);

		for (var i = 0; i < quantidade; i++)
		{
			parcelas.Add((i + 1, primeiraParcela.Date.AddMonths(i), valor));
		}

		return parcelas;
	}

	/// <summary>Valor da parcela gerada: maior que zero e dentro de VR_PCL.</summary>
	public static string? ValidarValorParcela(decimal valor) =>
		valor <= 0
			? "O valor da parcela deve ser maior que zero."
			: Monetario(valor, Valor11, "valor");

	/// <summary>Tipo de baixa da parcela (T009PAGA.ID_PGT_AUT).</summary>
	public const decimal BaixaManual = 1;
	public const decimal BaixaAutomatica = 2;
	public const decimal SemBaixa = 3;

	/// <summary>Forma de pagamento gravada na baixa manual (TP_PGT = OUTROS).</summary>
	public const decimal FormaPagamentoOutros = 5;

	/// <summary>Parâmetro (T009PARM.SQ_PRM) que exige saldo zero na parcela paga.</summary>
	public const decimal ParametroSaldoZero = 1;

	public static string? DescricaoTipoBaixa(decimal? tipo) => tipo switch
	{
		BaixaManual => "MANUAL",
		BaixaAutomatica => "AUTOMÁTICA",
		SemBaixa => "SEM BAIXA",
		_ => null
	};

	/// <summary>
	/// Validação da parcela enviada (pfc_validation de o_tc_t009paga).
	/// </summary>
	public static string? ValidarParcela(ParcelaDados? d)
	{
		if (d is null) return "Informe os dados da parcela.";
		if (d.DataVencimento is null) return "A data de vencimento (dataVencimento) é obrigatória.";
		if (d.Valor is null) return "O valor da parcela (valor) é obrigatório.";

		if ((Monetario(d.Valor, Valor11, "valor") ?? Monetario(d.ValorPago, Valor11, "valorPago")) is string erro)
		{
			return erro;
		}

		if (d.DataPagamento is not null && d.ValorPago is not > 0)
			return "Informe o valor do pagamento (valorPago) da parcela com data de pagamento.";
		if (d.ValorPago is > 0 && d.DataPagamento is null)
			return "Informe a data do pagamento (dataPagamento) da parcela com valor pago.";
		return null;
	}

	/// <summary>
	/// Baixa manual (itemchanged de vr_pgt): com valor pago, saldo = pago -
	/// valor, baixa MANUAL e forma OUTROS; sem valor pago, saldo = -valor,
	/// SEM BAIXA e forma nula, valor pago 0.
	/// </summary>
	public static (decimal ValorPago, decimal Saldo, decimal TipoBaixa, decimal? Forma) Pagamento(decimal valor, decimal? valorPago) =>
		valorPago is decimal pago and > 0
			? (pago, pago - valor, BaixaManual, FormaPagamentoOutros)
			: (0, -valor, SemBaixa, null);

	/// <summary>
	/// Parâmetro 1 ligado: parcela paga tem de ficar com saldo zero.
	/// </summary>
	public static string? ValidarSaldoZero(bool parametroLigado, decimal? valorPago, decimal? saldo) =>
		parametroLigado && valorPago is > 0 && saldo is not 0
			? "Saldo do pagamento inválido: com o parâmetro 1 da filial ligado, o valor pago deve ser igual ao valor da parcela."
			: null;

	/// <summary>
	/// Parcela paga (oe_delete): tipo de baixa preenchido e diferente de 3.
	/// </summary>
	public static bool ParcelaPaga(decimal? tipoBaixa) => tipoBaixa is not null && tipoBaixa != SemBaixa;

	/// <summary>Contrato quitado (9): parcelas não são incluídas, alteradas nem excluídas.</summary>
	public static bool ParcelasTravadas(decimal? situacaoContrato) => situacaoContrato == SituacaoQuitado;
}
