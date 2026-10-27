using SigeraAssociados.Application.Enderecos;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato do endereço do associado.
/// </summary>
public sealed class ValidacaoEnderecoAssociadoTests
{
	private static EnderecoAssociadoDados Valido(
		decimal? cdTipo = 1,
		string? logradouro = "RUA SAO PEDRO",
		string? cep = "63010-000",
		decimal? cdMunicipio = 2307304,
		decimal? numero = 120) => new()
	{
		CdTipo = cdTipo,
		Logradouro = logradouro,
		Numero = numero,
		Cep = cep,
		CdMunicipio = cdMunicipio
	};

	[Fact]
	public void EnderecoCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoEnderecoAssociado.Validar(Valido()));
	}

	[Fact]
	public void SemCepENumero_DeveSerValido()
	{
		Assert.Null(ValidacaoEnderecoAssociado.Validar(Valido(cep: null, numero: null)));
	}

	[Fact]
	public void SemTipo_DeveSerRecusado()
	{
		Assert.Contains("cdTipo", ValidacaoEnderecoAssociado.Validar(Valido(cdTipo: null)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("  ")]
	public void SemLogradouro_DeveSerRecusado(string? logradouro)
	{
		Assert.Contains("logradouro", ValidacaoEnderecoAssociado.Validar(Valido(logradouro: logradouro)));
	}

	[Fact]
	public void LogradouroComMaisDe50_DeveSerRecusado()
	{
		Assert.Contains("50", ValidacaoEnderecoAssociado.Validar(Valido(logradouro: new string('A', 51))));
	}

	[Fact]
	public void SemMunicipio_DeveSerRecusado()
	{
		Assert.Contains("cdMunicipio", ValidacaoEnderecoAssociado.Validar(Valido(cdMunicipio: null)));
	}

	[Theory]
	[InlineData("6301-000")]
	[InlineData("63010-0000")]
	[InlineData("ABCDE-FGH")]
	public void CepInvalido_DeveSerRecusado(string cep)
	{
		Assert.Contains("cep", ValidacaoEnderecoAssociado.Validar(Valido(cep: cep)));
	}

	[Fact]
	public void NumeroComMaisDe10Digitos_DeveSerRecusado()
	{
		Assert.Contains("numero", ValidacaoEnderecoAssociado.Validar(Valido(numero: 12_345_678_901m)));
	}

	[Fact]
	public void SemDados_DeveSerRecusado()
	{
		Assert.NotNull(ValidacaoEnderecoAssociado.Validar(null));
	}
}
