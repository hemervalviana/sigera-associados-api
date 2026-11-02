using SigeraAssociados.Application.Contratos;
using Xunit;

namespace SigeraAssociados.UnitTests;

public sealed class ValidacaoApoioContratoTests
{
	[Fact]
	public void Carencia_CodigoDe1A9_DescricaoAte30()
	{
		Assert.Null(ValidacaoApoioContrato.Carencia(new CarenciaDados { Descricao = "SEM CARÊNCIA" }, inclusao: true));
		Assert.Null(ValidacaoApoioContrato.Carencia(new CarenciaDados { CdCarencia = 9, Descricao = "X" }, inclusao: true));
		Assert.Contains("cdCarencia", ValidacaoApoioContrato.Carencia(new CarenciaDados { CdCarencia = 10, Descricao = "X" }, inclusao: true));
		Assert.Contains("descricao", ValidacaoApoioContrato.Carencia(new CarenciaDados { Descricao = new string('A', 31) }, inclusao: false));
		Assert.Contains("descricao", ValidacaoApoioContrato.Carencia(new CarenciaDados(), inclusao: false));
	}

	[Fact]
	public void Origem_ExigeDescricaoETipoDeUmCaractere()
	{
		Assert.Null(ValidacaoApoioContrato.OrigemRecurso(new OrigemRecursoDados { Descricao = "PROPRIO", Tipo = "P" }, inclusao: true));
		Assert.Contains("tipo", ValidacaoApoioContrato.OrigemRecurso(new OrigemRecursoDados { Descricao = "PROPRIO" }, inclusao: true));
		Assert.Contains("tipo", ValidacaoApoioContrato.OrigemRecurso(new OrigemRecursoDados { Descricao = "PROPRIO", Tipo = "PP" }, inclusao: true));
		Assert.Equal("P", ValidacaoApoioContrato.Codigo(" p "));
	}

	[Fact]
	public void Produtor_ExigeNomeECorretor()
	{
		Assert.Null(ValidacaoApoioContrato.Produtor(new ProdutorDados { Descricao = "JOSE", SqPesCorretor = 678 }, inclusao: true));
		Assert.Contains("sqPesCorretor", ValidacaoApoioContrato.Produtor(new ProdutorDados { Descricao = "JOSE" }, inclusao: true));
		Assert.Contains("descricao", ValidacaoApoioContrato.Produtor(new ProdutorDados { SqPesCorretor = 678 }, inclusao: false));
	}

	[Theory]
	[InlineData(0.0471, true)]
	[InlineData(9.9999, true)]
	[InlineData(0, false)]
	[InlineData(10, false)]
	[InlineData(0.12345, false)]
	public void Indice_MaiorQueZeroAte9_9999ComQuatroCasas(double indice, bool valido)
	{
		var d = new IndiceDados { CdCarencia = 1, QuantidadeParcelas = 24, Indice = (decimal)indice };
		Assert.Equal(valido, ValidacaoApoioContrato.Indice(d, inclusao: true) is null);
	}

	[Fact]
	public void Indice_InclusaoExigeCarenciaEParcelas_AlteracaoNao()
	{
		Assert.Contains("cdCarencia", ValidacaoApoioContrato.Indice(new IndiceDados { QuantidadeParcelas = 24, Indice = 0.05m }, inclusao: true));
		Assert.Contains("quantidadeParcelas", ValidacaoApoioContrato.Indice(new IndiceDados { CdCarencia = 1, Indice = 0.05m }, inclusao: true));
		Assert.Null(ValidacaoApoioContrato.Indice(new IndiceDados { Indice = 0.05m }, inclusao: false));
	}

	[Fact]
	public void Situacao_CodigoDe1A99_DescricaoAte50()
	{
		Assert.Null(ValidacaoApoioContrato.Situacao(new SituacaoContratoApoioDados { Descricao = "EM ANÁLISE" }, inclusao: true));
		Assert.Contains("cdSituacao", ValidacaoApoioContrato.Situacao(new SituacaoContratoApoioDados { CdSituacao = 100, Descricao = "X" }, inclusao: true));
		Assert.Contains("descricao", ValidacaoApoioContrato.Situacao(new SituacaoContratoApoioDados { Descricao = new string('A', 51) }, inclusao: false));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(9)]
	[InlineData(10)]
	[InlineData(12)]
	[InlineData(13)]
	public void SituacoesFixas_NaoPodemSerExcluidas(int codigo)
	{
		Assert.Contains((decimal)codigo, ValidacaoApoioContrato.SituacoesFixas);
	}

	[Fact]
	public void OutrasSituacoes_NaoSaoFixas()
	{
		Assert.DoesNotContain(1m, ValidacaoApoioContrato.SituacoesFixas);
		Assert.DoesNotContain(7m, ValidacaoApoioContrato.SituacoesFixas);
	}
}
