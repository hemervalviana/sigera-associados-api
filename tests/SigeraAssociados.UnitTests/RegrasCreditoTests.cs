using SigeraAssociados.Application.Creditos;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras do crédito do associado copiadas do legado:
/// saldo pela forma de pagamento, exclusão só com valor = saldo e formatos.
/// </summary>
public sealed class RegrasCreditoTests
{
	private static CreditoDados Credito(
		decimal? valor = 150m,
		decimal? forma = 1,
		decimal? tipoBaixa = null,
		decimal? cdFil = null,
		decimal? sqContrato = null,
		decimal? cdConvenio = null,
		decimal? cdAverbacao = null,
		DateTime? data = null) => new()
	{
		Valor = valor,
		CdFormaPagamento = forma,
		TipoBaixa = tipoBaixa,
		CdFil = cdFil,
		SqContrato = sqContrato,
		CdConvenio = cdConvenio,
		CdAverbacao = cdAverbacao,
		DataCredito = data
	};

	// ----- Saldo (itemchanged do legado) -----

	[Fact]
	public void FormaQueGeraSaldo_SaldoIgualAoValor()
	{
		Assert.Equal(150m, RegrasCredito.CalcularSaldo(150m, 1, idGeraSaldo: 1));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void FormaQueNaoGeraSaldo_SaldoZero(int idGeraSaldo)
	{
		Assert.Equal(0m, RegrasCredito.CalcularSaldo(150m, 1, idGeraSaldo));
	}

	[Fact]
	public void FormaSemIndicador_SaldoZero()
	{
		Assert.Equal(0m, RegrasCredito.CalcularSaldo(150m, 1, idGeraSaldo: null));
	}

	[Fact]
	public void SemForma_SaldoIgualAoValor()
	{
		Assert.Equal(80m, RegrasCredito.CalcularSaldo(80m, 0, idGeraSaldo: null));
	}

	[Fact]
	public void ValorZero_SaldoZero()
	{
		Assert.Equal(0m, RegrasCredito.CalcularSaldo(0m, 1, idGeraSaldo: 1));
	}

	// ----- Exclusão (oe_delete do legado) -----

	[Theory]
	[InlineData(150, 150)]
	[InlineData(0, 0)]
	public void ValorIgualAoSaldo_PodeExcluir(double valor, double saldo)
	{
		Assert.True(RegrasCredito.PodeExcluir((decimal)valor, (decimal)saldo));
	}

	[Theory]
	[InlineData(150, 0)]
	[InlineData(150, 100)]
	public void ValorDiferenteDoSaldo_NaoPodeExcluir(double valor, double saldo)
	{
		Assert.False(RegrasCredito.PodeExcluir((decimal)valor, (decimal)saldo));
	}

	[Fact]
	public void VazioContaComoZero()
	{
		Assert.True(RegrasCredito.PodeExcluir(null, 0));
		Assert.True(RegrasCredito.PodeExcluir(0, null));
		Assert.False(RegrasCredito.PodeExcluir(null, 10));
	}

	// ----- Validação -----

	[Fact]
	public void CreditoCompleto_DeveSerValido()
	{
		Assert.Null(RegrasCredito.Validar(inclusao: true, dados: Credito(tipoBaixa: 4, cdFil: 1, sqContrato: 10, cdConvenio: 2, cdAverbacao: 5)));
	}

	[Fact]
	public void ValorOmitidoNaInclusao_DeveSerRecusado()
	{
		Assert.Contains("obrigatório", RegrasCredito.Validar(inclusao: true, dados: Credito(valor: null)));
	}

	[Fact]
	public void ValorOmitidoNaAlteracao_DeveSerValido()
	{
		Assert.Null(RegrasCredito.Validar(inclusao: false, dados: Credito(valor: null)));
	}

	[Theory]
	[InlineData(0, true)]
	[InlineData(-1, true)]
	[InlineData(0, false)]
	[InlineData(-0.01, false)]
	public void ValorMenorOuIgualAZero_DeveSerRecusado(double valor, bool inclusao)
	{
		Assert.Contains("maior que zero", RegrasCredito.Validar(inclusao: inclusao, dados: Credito(valor: (decimal)valor)));
	}

	[Fact]
	public void ValorDeUmCentavo_DeveSerValido()
	{
		Assert.Null(RegrasCredito.Validar(inclusao: true, dados: Credito(valor: 0.01m)));
	}

	// ----- Alteração: valor e forma travados depois que o saldo é utilizado -----

	[Fact]
	public void SaldoIntacto_PodeMudarValor()
	{
		Assert.Null(RegrasCredito.ValidarAlteracao(valorAtual: 150m, saldoAtual: 150m, formaAtual: 1, valorNovo: 200m, formaNova: 1));
	}

	[Fact]
	public void SaldoIntacto_PodeMudarForma()
	{
		Assert.Null(RegrasCredito.ValidarAlteracao(valorAtual: 150m, saldoAtual: 150m, formaAtual: 1, valorNovo: 150m, formaNova: 2));
	}

	[Fact]
	public void SaldoUtilizado_NaoPodeMudarValor()
	{
		Assert.Contains("já foi utilizado",
			RegrasCredito.ValidarAlteracao(valorAtual: 150m, saldoAtual: 100m, formaAtual: 1, valorNovo: 200m, formaNova: 1));
	}

	[Fact]
	public void SaldoZerado_NaoPodeMudarValor()
	{
		Assert.NotNull(RegrasCredito.ValidarAlteracao(valorAtual: 150m, saldoAtual: 0m, formaAtual: 1, valorNovo: 200m, formaNova: 1));
	}

	[Fact]
	public void SaldoUtilizado_NaoPodeMudarForma()
	{
		Assert.NotNull(RegrasCredito.ValidarAlteracao(valorAtual: 150m, saldoAtual: 100m, formaAtual: 1, valorNovo: 150m, formaNova: 2));
	}

	[Fact]
	public void SaldoUtilizado_PodeAlterarOutrosCampos()
	{
		// Mesmo valor e mesma forma: observação, data etc. continuam livres.
		Assert.Null(RegrasCredito.ValidarAlteracao(valorAtual: 150m, saldoAtual: 100m, formaAtual: 1, valorNovo: 150m, formaNova: 1));
	}

	[Theory]
	[InlineData(150, 150, true)]
	[InlineData(150, 100, false)]
	[InlineData(150, 0, false)]
	public void SaldoIntacto_SoQuandoSaldoIgualAoValor(double valor, double saldo, bool intacto)
	{
		Assert.Equal(intacto, RegrasCredito.SaldoIntacto((decimal)valor, (decimal)saldo));
	}

	[Fact]
	public void ValorComTresCasas_DeveSerRecusado()
	{
		Assert.Contains("2 casas", RegrasCredito.Validar(inclusao: true, dados: Credito(valor: 10.555m)));
	}

	[Fact]
	public void SemFormaDePagamento_DeveSerRecusado()
	{
		Assert.Contains("cdFormaPagamento", RegrasCredito.Validar(inclusao: true, dados: Credito(forma: null)));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	public void TipoBaixaDoLegado_DeveSerValido(int tipo)
	{
		Assert.Null(RegrasCredito.Validar(inclusao: true, dados: Credito(tipoBaixa: tipo)));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	[InlineData(5)]
	public void TipoBaixaDesconhecido_DeveSerRecusado(int tipo)
	{
		Assert.Contains("tipoBaixa", RegrasCredito.Validar(inclusao: true, dados: Credito(tipoBaixa: tipo)));
	}

	[Fact]
	public void ContratoSemFilial_DeveSerRecusado()
	{
		Assert.Contains("cdFil", RegrasCredito.Validar(inclusao: true, dados: Credito(sqContrato: 10)));
	}

	[Fact]
	public void AverbacaoSemConvenio_DeveSerRecusada()
	{
		Assert.Contains("cdConvenio", RegrasCredito.Validar(inclusao: true, dados: Credito(cdFil: 1, cdAverbacao: 5)));
	}

	[Fact]
	public void DataForaDaFaixa_DeveSerRecusada()
	{
		Assert.Contains("dataCredito", RegrasCredito.Validar(inclusao: true, dados: Credito(data: new DateTime(1899, 12, 31))));
	}

	[Fact]
	public void SemDados_DeveSerRecusado()
	{
		Assert.NotNull(RegrasCredito.Validar(null, inclusao: true));
	}

	[Fact]
	public void DescricaoTipoBaixa_DeveTrazerOsValoresDoLegado()
	{
		Assert.Equal("AUTOMÁTICA", RegrasCredito.DescricaoTipoBaixa(1));
		Assert.Equal("MANUAL", RegrasCredito.DescricaoTipoBaixa(2));
		Assert.Equal("CRÉDITO", RegrasCredito.DescricaoTipoBaixa(4));
		Assert.Null(RegrasCredito.DescricaoTipoBaixa(3));
	}
}
