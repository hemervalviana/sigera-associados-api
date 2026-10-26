using SigeraAssociados.Application.Cadastros;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato da manutenção das tabelas de apoio.
/// </summary>
public sealed class ValidacaoTabelasApoioTests
{
	[Fact]
	public void FilialMinima_DeveSerValida()
	{
		Assert.Null(ValidacaoTabelasApoio.Filial(new FilialDados { Nome = "FILIAL CRATO" }, inclusao: true));
	}

	[Fact]
	public void FilialCompleta_DeveSerValida()
	{
		var filial = new FilialDados
		{
			CdFil = 3,
			Nome = "FILIAL CRATO",
			Cep = "63100-000",
			Uf = "ce",
			Ddd = 88,
			Cnpj = "11.222.333/0001-81"
		};

		Assert.Null(ValidacaoTabelasApoio.Filial(filial, inclusao: true));
	}

	[Fact]
	public void FilialSemNome_DeveSerRecusada()
	{
		Assert.Contains("nome", ValidacaoTabelasApoio.Filial(new FilialDados(), inclusao: true));
	}

	[Theory]
	[InlineData("6310-000")]
	[InlineData("63100-0000")]
	[InlineData("ABCDE-FGH")]
	public void CepInvalido_DeveSerRecusado(string cep)
	{
		Assert.Contains("cep", ValidacaoTabelasApoio.Filial(new FilialDados { Nome = "X", Cep = cep }, inclusao: true));
	}

	[Theory]
	[InlineData("11.222.333/0001-82")]
	[InlineData("11.111.111/1111-11")]
	[InlineData("123")]
	public void CnpjInvalido_DeveSerRecusado(string cnpj)
	{
		Assert.NotNull(ValidacaoTabelasApoio.Filial(new FilialDados { Nome = "X", Cnpj = cnpj }, inclusao: true));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	public void CodigoDeFilialForaDaFaixa_DeveSerRecusado(int codigo)
	{
		Assert.Contains("cdFil", ValidacaoTabelasApoio.Filial(new FilialDados { CdFil = codigo, Nome = "X" }, inclusao: true));
	}

	[Fact]
	public void NaAlteracao_OCodigoDoCorpoEIgnorado()
	{
		Assert.Null(ValidacaoTabelasApoio.Filial(new FilialDados { CdFil = 500, Nome = "X" }, inclusao: false));
	}

	[Fact]
	public void Item_SemDescricao_DeveSerRecusado()
	{
		Assert.Contains("descricao",
			ValidacaoTabelasApoio.Item(new ItemTabelaDados(), ValidacaoTabelasApoio.TamanhoSituacao, inclusao: true));
	}

	[Fact]
	public void Item_DescricaoLonga_DeveSerRecusada()
	{
		var dados = new ItemTabelaDados { Descricao = new string('A', 21) };

		Assert.Null(ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoSituacao, inclusao: true));
		Assert.Contains("20", ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTratamento, inclusao: true));
	}

	[Fact]
	public void Municipio_SemCodigoNaInclusao_DeveSerRecusado()
	{
		Assert.Contains("cdMun",
			ValidacaoTabelasApoio.Municipio(new MunicipioDados { Nome = "CRATO", Uf = "CE" }, inclusao: true));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("C")]
	[InlineData("C1")]
	public void Municipio_UfInvalida_DeveSerRecusado(string? uf)
	{
		Assert.Contains("UF",
			ValidacaoTabelasApoio.Municipio(new MunicipioDados { CdMun = 2304202, Nome = "CRATO", Uf = uf }, inclusao: true));
	}

	[Fact]
	public void MunicipioCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoTabelasApoio.Municipio(
			new MunicipioDados { CdMun = 2307304, Nome = "JUAZEIRO DO NORTE", Uf = "CE" }, inclusao: true));
	}
}
