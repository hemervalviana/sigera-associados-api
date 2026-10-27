using SigeraAssociados.Application.Telefones;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato do telefone do associado.
/// </summary>
public sealed class ValidacaoTelefoneAssociadoTests
{
	private static TelefoneAssociadoDados Valido(
		decimal? ddd = 88,
		string? numero = "99999-1234",
		decimal? cdTipo = 2) => new()
	{
		Ddd = ddd,
		Numero = numero,
		CdTipo = cdTipo
	};

	[Fact]
	public void TelefoneCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoTelefoneAssociado.Validar(Valido()));
	}

	[Theory]
	[InlineData("35211234")]
	[InlineData("3521-1234")]
	[InlineData("999991234")]
	public void NumeroCom8Ou9Digitos_DeveSerValido(string numero)
	{
		Assert.Null(ValidacaoTelefoneAssociado.Validar(Valido(numero: numero)));
	}

	[Fact]
	public void SemTipo_DeveSerValido()
	{
		Assert.Null(ValidacaoTelefoneAssociado.Validar(Valido(cdTipo: null)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData(10)]
	[InlineData(100)]
	public void DddInvalido_DeveSerRecusado(int? ddd)
	{
		Assert.Contains("ddd", ValidacaoTelefoneAssociado.Validar(Valido(ddd: ddd)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("1234567")]
	[InlineData("1234567890")]
	[InlineData("(88)99999-1234")]
	[InlineData("9999A1234")]
	public void NumeroInvalido_DeveSerRecusado(string? numero)
	{
		Assert.Contains("numero", ValidacaoTelefoneAssociado.Validar(Valido(numero: numero)));
	}

	[Fact]
	public void TipoForaDaFaixa_DeveSerRecusado()
	{
		Assert.Contains("cdTipo", ValidacaoTelefoneAssociado.Validar(Valido(cdTipo: 100)));
	}

	[Fact]
	public void SemDados_DeveSerRecusado()
	{
		Assert.NotNull(ValidacaoTelefoneAssociado.Validar(null));
	}

	[Fact]
	public void Normalizar_DeveDeixarSoOsDigitos()
	{
		Assert.Equal("999991234", ValidacaoTelefoneAssociado.Normalizar("99999-1234"));
	}

	[Theory]
	[InlineData(88, "999991234", "(88) 99999-1234")]
	[InlineData(88, "35211234", "(88) 3521-1234")]
	[InlineData(88, "3521-1234", "(88) 3521-1234")]
	[InlineData(88, "12345", "(88) 12345")]
	public void Formatar_DeveMontarDddENumero(int ddd, string numero, string esperado)
	{
		Assert.Equal(esperado, ValidacaoTelefoneAssociado.Formatar(ddd, numero));
	}
}
