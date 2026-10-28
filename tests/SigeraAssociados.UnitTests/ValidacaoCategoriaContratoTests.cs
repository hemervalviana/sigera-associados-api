using SigeraAssociados.Application.Contratos;
using Xunit;

namespace SigeraAssociados.UnitTests;

public sealed class ValidacaoCategoriaContratoTests
{
	[Fact]
	public void CategoriaComNome_DeveSerValida()
	{
		Assert.Null(ValidacaoCategoriaContrato.Validar(new CategoriaContratoDados { Descricao = "SERVIDOR ESTADUAL" }, inclusao: true));
		Assert.Null(ValidacaoCategoriaContrato.Validar(new CategoriaContratoDados { CdCategoria = 999, Descricao = "X" }, inclusao: true));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SemNome_DeveSerRecusada(string? descricao)
	{
		Assert.Contains("descricao", ValidacaoCategoriaContrato.Validar(new CategoriaContratoDados { Descricao = descricao }, inclusao: true));
	}

	[Fact]
	public void NomeComMaisDe35_DeveSerRecusado()
	{
		Assert.Contains("descricao", ValidacaoCategoriaContrato.Validar(new CategoriaContratoDados { Descricao = new string('A', 36) }, inclusao: false));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1000)]
	public void CodigoForaDe1A999_DeveSerRecusadoNaInclusao(int codigo)
	{
		Assert.Contains("cdCategoria", ValidacaoCategoriaContrato.Validar(new CategoriaContratoDados { CdCategoria = codigo, Descricao = "X" }, inclusao: true));
	}

	[Fact]
	public void SemDados_DeveSerRecusada()
	{
		Assert.NotNull(ValidacaoCategoriaContrato.Validar(null, inclusao: true));
	}
}
