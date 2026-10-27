using SigeraAssociados.Application.Financeiro;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato de formas de pagamento, produtos e convênios.
/// </summary>
public sealed class ValidacaoCadastrosFinanceirosTests
{
	[Fact]
	public void FormaPagamentoCompleta_DeveSerValida()
	{
		Assert.Null(ValidacaoFormaPagamento.Validar(new FormaPagamentoDados { Descricao = "DINHEIRO", GeraSaldo = true }, inclusao: true));
	}

	[Fact]
	public void FormaPagamentoSemDescricao_DeveSerRecusada()
	{
		Assert.Contains("descricao", ValidacaoFormaPagamento.Validar(new FormaPagamentoDados(), inclusao: true));
	}

	[Fact]
	public void FormaPagamentoComCodigoAcimaDe99_DeveSerRecusada()
	{
		Assert.Contains("codigo", ValidacaoFormaPagamento.Validar(new FormaPagamentoDados { Codigo = 100, Descricao = "X" }, inclusao: true));
	}

	[Fact]
	public void ProdutoCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoProduto.Validar(new ProdutoDados { Descricao = "PLANO FAMILIAR" }, inclusao: true));
	}

	[Fact]
	public void ProdutoComDescricaoLonga_DeveSerRecusado()
	{
		Assert.Contains("100", ValidacaoProduto.Validar(new ProdutoDados { Descricao = new string('A', 101) }, inclusao: false));
	}

	[Fact]
	public void ConvenioSoComNome_DeveSerValido()
	{
		Assert.Null(ValidacaoConvenio.Validar(new ConvenioDados { Nome = "CONSIGNADO SEDUC" }, inclusao: true));
	}

	[Fact]
	public void ConvenioCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoConvenio.Validar(new ConvenioDados
		{
			Nome = "CONSIGNADO SEDUC",
			Numero = "123456",
			CdFil = 1,
			CdBanco = 104,
			SqContaCorrente = 1,
			CodigoEmpresa = 12_345_678_901_234_567_890m,
			Carteira = 17,
			CodigoCompensacao = 1234,
			CodigoCnab = 240,
			ParametroTransmissao = 1,
			TipoGoverno = 1,
			DiretorioRemessa = "C:\\CONVENIOS\\REMESSA",
			ExtensaoArquivo = "TXT",
			CdSituacao = 1
		}, inclusao: true));
	}

	[Fact]
	public void ConvenioSemNome_DeveSerRecusado()
	{
		Assert.Contains("nome", ValidacaoConvenio.Validar(new ConvenioDados(), inclusao: true));
	}

	[Fact]
	public void ConvenioComNomeAcimaDe35_DeveSerRecusado()
	{
		Assert.Contains("35", ValidacaoConvenio.Validar(new ConvenioDados { Nome = new string('A', 36) }, inclusao: true));
	}

	[Theory]
	[InlineData("extensaoArquivo")]
	[InlineData("cdSituacao")]
	[InlineData("codigoCompensacao")]
	public void ConvenioComCampoForaDoTamanho_DeveSerRecusado(string campo)
	{
		var dados = campo switch
		{
			"extensaoArquivo" => new ConvenioDados { Nome = "X", ExtensaoArquivo = "ABCDEF" },
			"cdSituacao" => new ConvenioDados { Nome = "X", CdSituacao = 10 },
			_ => new ConvenioDados { Nome = "X", CodigoCompensacao = 10_000 }
		};

		Assert.Contains(campo, ValidacaoConvenio.Validar(dados, inclusao: false));
	}
}
