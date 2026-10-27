using SigeraAssociados.Application.Pix;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato da chave Pix do associado.
/// </summary>
public sealed class ValidacaoChavePixTests
{
	private static ChavePixDados Chave(short? tipo, string? chave) => new() { CdTipo = tipo, Chave = chave };

	[Theory]
	[InlineData(1, "123.456.789-09")]
	[InlineData(1, "12345678909")]
	[InlineData(1, "11.222.333/0001-81")]
	[InlineData(2, "+55 (88) 99999-1234")]
	[InlineData(2, "88999991234")]
	[InlineData(3, "fulano.tal@exemplo.com.br")]
	[InlineData(4, "123e4567-e89b-12d3-a456-426614174000")]
	public void ChaveValida_DeveSerAceita(short tipo, string chave)
	{
		Assert.Null(ValidacaoChavePix.Validar(Chave(tipo, chave)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData((short)0)]
	[InlineData((short)5)]
	public void TipoInvalido_DeveSerRecusado(short? tipo)
	{
		Assert.Contains("cdTipo", ValidacaoChavePix.Validar(Chave(tipo, "12345678909")));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SemChave_DeveSerRecusada(string? chave)
	{
		Assert.Contains("chave", ValidacaoChavePix.Validar(Chave(1, chave)));
	}

	[Theory]
	[InlineData("123.456.789-00")]
	[InlineData("111.111.111-11")]
	[InlineData("11.222.333/0001-00")]
	[InlineData("1234567890")]
	[InlineData("123456789A9")]
	public void CpfCnpjInvalido_DeveSerRecusado(string chave)
	{
		Assert.NotNull(ValidacaoChavePix.Validar(Chave(TipoChavePix.CpfCnpj, chave)));
	}

	[Theory]
	[InlineData("8899999123")]
	[InlineData("88399991234")]
	[InlineData("0899999-1234")]
	[InlineData("88 99999-123A")]
	public void CelularInvalido_DeveSerRecusado(string chave)
	{
		Assert.NotNull(ValidacaoChavePix.Validar(Chave(TipoChavePix.Celular, chave)));
	}

	[Theory]
	[InlineData("sem-arroba.com")]
	[InlineData("fulano@semponto")]
	[InlineData("fulano tal@exemplo.com")]
	public void EmailInvalido_DeveSerRecusado(string chave)
	{
		Assert.NotNull(ValidacaoChavePix.Validar(Chave(TipoChavePix.Email, chave)));
	}

	[Fact]
	public void EmailMuitoLongo_DeveSerRecusado()
	{
		var email = new string('a', 70) + "@exemplo.com";

		Assert.Contains("77", ValidacaoChavePix.Validar(Chave(TipoChavePix.Email, email)));
	}

	[Theory]
	[InlineData("123e4567e89b12d3a456426614174000")]
	[InlineData("nao-e-uuid")]
	public void AleatoriaInvalida_DeveSerRecusada(string chave)
	{
		Assert.NotNull(ValidacaoChavePix.Validar(Chave(TipoChavePix.Aleatoria, chave)));
	}

	[Theory]
	[InlineData(1, "123.456.789-09", "12345678909")]
	[InlineData(2, "+55 (88) 99999-1234", "88999991234")]
	[InlineData(3, " Fulano@Exemplo.COM ", "fulano@exemplo.com")]
	[InlineData(4, "123E4567-E89B-12D3-A456-426614174000", "123e4567-e89b-12d3-a456-426614174000")]
	public void Normalizar_DeveGravarNoFormatoPadrao(short tipo, string chave, string esperado)
	{
		Assert.Equal(esperado, ValidacaoChavePix.Normalizar(tipo, chave));
	}

	[Theory]
	[InlineData(1, "12345678909", "123.456.789-09")]
	[InlineData(1, "11222333000181", "11.222.333/0001-81")]
	[InlineData(2, "88999991234", "(88) 99999-1234")]
	[InlineData(3, "fulano@exemplo.com", "fulano@exemplo.com")]
	[InlineData(1, "123.456.789-09", "123.456.789-09")]
	public void Formatar_DeveMontarAChaveParaExibicao(short tipo, string chave, string esperado)
	{
		Assert.Equal(esperado, ValidacaoChavePix.Formatar(tipo, chave));
	}

	[Fact]
	public void Descricao_DeveTrazerOsTiposDoLegado()
	{
		Assert.Equal("CPF/CNPJ", TipoChavePix.Descricao(1));
		Assert.Equal("CELULAR", TipoChavePix.Descricao(2));
		Assert.Equal("E-MAIL", TipoChavePix.Descricao(3));
		Assert.Equal("ALEATÓRIA", TipoChavePix.Descricao(4));
		Assert.Null(TipoChavePix.Descricao(9));
	}
}
