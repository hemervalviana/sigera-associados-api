using SigeraAssociados.Application.Referencias;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato da referência do associado.
/// </summary>
public sealed class ValidacaoReferenciaAssociadoTests
{
	private static ReferenciaAssociadoDados Valida(
		string? nome = "MARIA DA SILVA",
		DateTime? dataNascimento = null,
		string? telefone1 = "(88) 99999-1234",
		string? telefone2 = null) => new()
	{
		Nome = nome,
		DataNascimento = dataNascimento,
		Telefone1 = telefone1,
		Telefone2 = telefone2
	};

	[Fact]
	public void ReferenciaCompleta_DeveSerValida()
	{
		Assert.Null(ValidacaoReferenciaAssociado.Validar(
			Valida(dataNascimento: new DateTime(1970, 5, 20), telefone2: "8835211234")));
	}

	[Fact]
	public void SoNomeETelefone1_DeveSerValida()
	{
		Assert.Null(ValidacaoReferenciaAssociado.Validar(Valida()));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SemNome_DeveSerRecusada(string? nome)
	{
		Assert.Contains("nome", ValidacaoReferenciaAssociado.Validar(Valida(nome: nome)));
	}

	[Fact]
	public void NomeMuitoLongo_DeveSerRecusado()
	{
		Assert.Contains("50", ValidacaoReferenciaAssociado.Validar(Valida(nome: new string('A', 51))));
	}

	[Fact]
	public void SemTelefone1_DeveSerRecusada()
	{
		Assert.Contains("telefone1", ValidacaoReferenciaAssociado.Validar(Valida(telefone1: null)));
	}

	[Theory]
	[InlineData("99999-1234")]
	[InlineData("889999912345")]
	[InlineData("(08) 99999-1234")]
	[InlineData("88 9999A-1234")]
	public void Telefone1Invalido_DeveSerRecusado(string telefone)
	{
		Assert.Contains("telefone1", ValidacaoReferenciaAssociado.Validar(Valida(telefone1: telefone)));
	}

	[Fact]
	public void Telefone2Invalido_DeveSerRecusado()
	{
		Assert.Contains("telefone2", ValidacaoReferenciaAssociado.Validar(Valida(telefone2: "123")));
	}

	[Fact]
	public void NascimentoNoFuturo_DeveSerRecusado()
	{
		Assert.Contains("dataNascimento",
			ValidacaoReferenciaAssociado.Validar(Valida(dataNascimento: DateTime.Today.AddDays(1))));
	}

	[Fact]
	public void SemDados_DeveSerRecusada()
	{
		Assert.NotNull(ValidacaoReferenciaAssociado.Validar(null));
	}

	[Theory]
	[InlineData("(88) 99999-1234", "88999991234")]
	[InlineData(" ", null)]
	[InlineData(null, null)]
	public void Normalizar_DeveDeixarSoOsDigitos(string? telefone, string? esperado)
	{
		Assert.Equal(esperado, ValidacaoReferenciaAssociado.Normalizar(telefone));
	}

	[Theory]
	[InlineData("88999991234", "(88) 99999-1234")]
	[InlineData("8835211234", "(88) 3521-1234")]
	[InlineData("35211234", "35211234")]
	public void Formatar_DeveMontarDddENumero(string telefone, string esperado)
	{
		Assert.Equal(esperado, ValidacaoReferenciaAssociado.Formatar(telefone));
	}
}
