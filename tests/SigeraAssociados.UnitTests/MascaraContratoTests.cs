using SigeraAssociados.Infrastructure.Contratos;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>Busca de contrato na máscara do legado ##-###### (filial + número).</summary>
public sealed class MascaraContratoTests
{
	[Theory]
	[InlineData("01-000123", 1, 123)]
	[InlineData(" 12-999999 ", 12, 999999)]
	[InlineData("1-123", 1, 123)]
	public void Mascara_SeparaFilialENumero(string texto, int filial, int numero)
	{
		Assert.Equal(((decimal)filial, (decimal)numero), ContratosService.LerMascaraContrato(texto));
	}

	[Theory]
	[InlineData("01000123")]
	[InlineData("123-000123")]
	[InlineData("01-1234567")]
	[InlineData("01-")]
	[InlineData("AB-000123")]
	[InlineData("529.982.247-25")]
	public void ForaDaMascara_NaoEhContrato(string texto)
	{
		Assert.Null(ContratosService.LerMascaraContrato(texto));
	}
}
