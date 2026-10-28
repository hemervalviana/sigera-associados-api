using SigeraAssociados.Application.Contratos;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Regras do contrato e das parcelas copiadas do legado (w_r_t009ctrt).
/// </summary>
public sealed class RegrasContratoTests
{
	private static ContratoDados Valido(
		decimal? situacao = 2,
		DateTime? quitacao = null,
		DateTime? finalizacao = null,
		DateTime? cancelamento = null,
		DateTime? primeira = null,
		DateTime? dataContrato = null,
		decimal? quantidade = 12,
		decimal? valor = 1000m,
		decimal? indice = null,
		decimal? mensalidade = 100m,
		decimal? categoria = 1,
		decimal? cdFil = 1) => new()
	{
		CdFil = cdFil,
		SqPesCli = 123,
		CdProduto = 2,
		CdConvenio = 1,
		CdFormaPagamento = 1,
		CdSituacao = situacao,
		SqOrgao = 1,
		SqContaAssociado = 1,
		DataContrato = dataContrato ?? new DateTime(2026, 10, 27),
		DataPrimeiraParcela = primeira ?? new DateTime(2026, 12, 10),
		ValorContrato = valor,
		QuantidadeParcelas = quantidade,
		Indice = indice,
		ValorMensalidade = mensalidade,
		CdCategoria = categoria,
		DataQuitacao = quitacao,
		DataFinalizacao = finalizacao,
		DataCancelamento = cancelamento
	};

	[Fact]
	public void ContratoCompleto_DeveSerValido()
	{
		Assert.Null(RegrasContrato.Validar(Valido(), inclusao: true));
	}

	[Fact]
	public void SemDados_DeveSerRecusado()
	{
		Assert.NotNull(RegrasContrato.Validar(null, inclusao: true));
	}

	[Fact]
	public void Inclusao_ExigeFilialEQuantidade_AlteracaoNao()
	{
		Assert.Contains("cdFil", RegrasContrato.Validar(Valido(cdFil: null), inclusao: true));
		Assert.Contains("quantidadeParcelas", RegrasContrato.Validar(Valido(quantidade: null), inclusao: true));
		Assert.Null(RegrasContrato.Validar(Valido(cdFil: null, quantidade: null), inclusao: false));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void ValorDoContratoZeroOuNegativo_DeveSerRecusado(int valor)
	{
		Assert.Contains("valorContrato", RegrasContrato.Validar(Valido(valor: valor), inclusao: true));
	}

	[Fact]
	public void SemCategoria_DeveSerRecusado()
	{
		Assert.Contains("cdCategoria", RegrasContrato.Validar(Valido(categoria: null), inclusao: true));
	}

	[Theory]
	[InlineData(2026, 10, 27)] // igual à data do contrato
	[InlineData(2026, 10, 1)]  // antes
	public void PrimeiraParcelaAteADataDoContrato_DeveSerRecusada(int ano, int mes, int dia)
	{
		Assert.Contains("1ª parcela", RegrasContrato.Validar(Valido(primeira: new DateTime(ano, mes, dia)), inclusao: true));
	}

	[Fact]
	public void PrimeiraParcela_AlteracaoComparaComADataGravada()
	{
		Assert.NotNull(RegrasContrato.PrimeiraParcela(new DateTime(2026, 1, 5), new DateTime(2026, 1, 5, 14, 0, 0)));
		Assert.Null(RegrasContrato.PrimeiraParcela(new DateTime(2026, 1, 6), new DateTime(2026, 1, 5)));
	}

	// ----- Situação e datas -----

	[Theory]
	[InlineData(9)]
	[InlineData(13)]
	public void QuitadoSemReferenciaDeQuitacao_DeveSerRecusado(int situacao)
	{
		Assert.Contains("dataQuitacao", RegrasContrato.Validar(Valido(situacao: situacao), inclusao: true));
	}

	[Theory]
	[InlineData(9)]
	[InlineData(13)]
	public void QuitadoComReferencia_DeveSerValido(int situacao)
	{
		Assert.Null(RegrasContrato.Validar(Valido(situacao: situacao, quitacao: DateTime.Today), inclusao: true));
	}

	[Fact]
	public void ReferenciaDeQuitacaoSemSituacaoQuitado_DeveSerRecusada()
	{
		Assert.Contains("dataQuitacao", RegrasContrato.Validar(Valido(situacao: 2, quitacao: DateTime.Today), inclusao: true));
	}

	[Fact]
	public void Finalizado_ExigeDataDeFinalizacao_ESoComEle()
	{
		Assert.Contains("dataFinalizacao", RegrasContrato.Validar(Valido(situacao: 12), inclusao: true));
		Assert.Null(RegrasContrato.Validar(Valido(situacao: 12, finalizacao: DateTime.Today), inclusao: true));
		Assert.Contains("dataFinalizacao", RegrasContrato.Validar(Valido(situacao: 2, finalizacao: DateTime.Today), inclusao: true));
	}

	[Fact]
	public void Cancelado_ExigeDataDeCancelamento_ESoComEle()
	{
		Assert.Contains("dataCancelamento", RegrasContrato.Validar(Valido(situacao: 10), inclusao: true));
		Assert.Null(RegrasContrato.Validar(Valido(situacao: 10, cancelamento: DateTime.Today), inclusao: true));
		Assert.Contains("dataCancelamento", RegrasContrato.Validar(Valido(situacao: 2, cancelamento: DateTime.Today), inclusao: true));
	}

	[Fact]
	public void Encerramento_HojeQuandoPassaA4Ou9_LimpaNasOutras_MantemSemMudanca()
	{
		var hoje = new DateTime(2026, 10, 27, 15, 30, 0);
		var antiga = new DateTime(2026, 1, 1);

		Assert.Equal(hoje.Date, RegrasContrato.DataEncerramento(null, 4, null, hoje));
		Assert.Equal(hoje.Date, RegrasContrato.DataEncerramento(2, 9, null, hoje));
		Assert.Null(RegrasContrato.DataEncerramento(9, 2, antiga, hoje));
		Assert.Equal(antiga, RegrasContrato.DataEncerramento(9, 9, antiga, hoje));
	}

	// ----- Índice e mensalidade -----

	[Fact]
	public void IndiceSemMensalidade_CalculaMensalidade()
	{
		Assert.Equal((0.0525m, 52.50m), RegrasContrato.IndiceEMensalidade(1000m, 0.0525m, null, null));
	}

	[Fact]
	public void MensalidadeSemIndice_CalculaIndice()
	{
		Assert.Equal((0.0333m, 100m), RegrasContrato.IndiceEMensalidade(3000m, null, 100m, null));
	}

	[Fact]
	public void SemIndiceNemMensalidade_UsaOIndiceDaTabela()
	{
		Assert.Equal((0.1m, 500m), RegrasContrato.IndiceEMensalidade(5000m, null, null, 0.1m));
	}

	[Fact]
	public void MensalidadeInformada_NaoUsaOIndiceDaTabela()
	{
		Assert.Equal((0.04m, 200m), RegrasContrato.IndiceEMensalidade(5000m, null, 200m, 0.1m));
	}

	[Fact]
	public void IndicesInvalidos_SaoRecusados()
	{
		Assert.NotNull(RegrasContrato.Validar(Valido(indice: 10m), inclusao: true));
		Assert.NotNull(RegrasContrato.Validar(Valido(indice: 0.12345m), inclusao: true));
	}

	// ----- Geração das parcelas -----

	[Fact]
	public void GerarParcelas_MensalidadeMaisPeculio_MesAMes()
	{
		var parcelas = RegrasContrato.GerarParcelas(new DateTime(2026, 11, 10), 3, 100m, 15.5m);

		Assert.Equal(3, parcelas.Count);
		Assert.All(parcelas, p => Assert.Equal(115.5m, p.Valor));
		Assert.Equal(new[] { 1m, 2m, 3m }, parcelas.Select(p => p.Numero));
		Assert.Equal(
			new[] { new DateTime(2026, 11, 10), new DateTime(2026, 12, 10), new DateTime(2027, 1, 10) },
			parcelas.Select(p => p.Vencimento));
	}

	[Fact]
	public void GerarParcelas_Dia31EmMesCurto_VaiParaOUltimoDia()
	{
		var parcelas = RegrasContrato.GerarParcelas(new DateTime(2027, 1, 31), 3, 100m, null);

		Assert.Equal(
			new[] { new DateTime(2027, 1, 31), new DateTime(2027, 2, 28), new DateTime(2027, 3, 31) },
			parcelas.Select(p => p.Vencimento));
	}

	// ----- Parcelas (o_tc_t009paga) -----

	private static ParcelaDados Parcela(decimal? valor = 100m, DateTime? pagamento = null, decimal? pago = null) => new()
	{
		DataVencimento = new DateTime(2027, 1, 10),
		Valor = valor,
		DataPagamento = pagamento,
		ValorPago = pago
	};

	[Fact]
	public void Parcela_ExigeVencimentoEValor_ZeroEhAceito()
	{
		Assert.Contains("dataVencimento", RegrasContrato.ValidarParcela(new ParcelaDados { Valor = 10 }));
		Assert.Contains("valor", RegrasContrato.ValidarParcela(new ParcelaDados { DataVencimento = DateTime.Today }));
		Assert.Null(RegrasContrato.ValidarParcela(Parcela(valor: 0)));
		Assert.NotNull(RegrasContrato.ValidarParcela(Parcela(valor: -1)));
		Assert.NotNull(RegrasContrato.ValidarParcela(Parcela(valor: 10.555m)));
	}

	[Fact]
	public void DataDePagamentoSemValorPago_DeveSerRecusada()
	{
		Assert.Contains("valorPago", RegrasContrato.ValidarParcela(Parcela(pagamento: DateTime.Today)));
		Assert.Contains("valorPago", RegrasContrato.ValidarParcela(Parcela(pagamento: DateTime.Today, pago: 0)));
	}

	[Fact]
	public void ValorPagoSemData_DeveSerRecusado()
	{
		Assert.Contains("dataPagamento", RegrasContrato.ValidarParcela(Parcela(pago: 50)));
	}

	[Fact]
	public void PagamentoCompleto_DeveSerValido()
	{
		Assert.Null(RegrasContrato.ValidarParcela(Parcela(pagamento: DateTime.Today, pago: 100)));
	}

	[Fact]
	public void Pagamento_BaixaManualComFormaOutros()
	{
		Assert.Equal((80m, -20m, 1m, (decimal?)5m), RegrasContrato.Pagamento(100m, 80m));
		Assert.Equal((120m, 20m, 1m, (decimal?)5m), RegrasContrato.Pagamento(100m, 120m));
	}

	[Fact]
	public void SemValorPago_SemBaixa()
	{
		Assert.Equal((0m, -100m, 3m, (decimal?)null), RegrasContrato.Pagamento(100m, null));
		Assert.Equal((0m, -100m, 3m, (decimal?)null), RegrasContrato.Pagamento(100m, 0m));
	}

	[Fact]
	public void ParametroDoSaldoZero_SoValeParaParcelaPaga()
	{
		Assert.NotNull(RegrasContrato.ValidarSaldoZero(true, 80m, -20m));
		Assert.NotNull(RegrasContrato.ValidarSaldoZero(true, 120m, 20m));
		Assert.Null(RegrasContrato.ValidarSaldoZero(true, 100m, 0m));
		Assert.Null(RegrasContrato.ValidarSaldoZero(true, 0m, -100m));
		Assert.Null(RegrasContrato.ValidarSaldoZero(false, 80m, -20m));
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData(3, false)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	public void ParcelaPaga_TipoDeBaixaDiferenteDe3(int? tipo, bool paga)
	{
		Assert.Equal(paga, RegrasContrato.ParcelaPaga(tipo));
	}

	[Fact]
	public void ContratoQuitado_TravaAsParcelas()
	{
		Assert.True(RegrasContrato.ParcelasTravadas(9));
		Assert.False(RegrasContrato.ParcelasTravadas(13));
		Assert.False(RegrasContrato.ParcelasTravadas(null));
	}
}
