using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Acesso;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de um novo escopo de acesso
/// (GestaoAcessosService.Validar).
/// </summary>
public sealed class ValidacaoAcessoTests
{
	private static string? Validar(
		int sistema,
		decimal? cdEmp = null,
		decimal? cdFil = null,
		decimal? sqPesCorretor = null)
	{
		return GestaoAcessosService.Validar(new NovoAcessoDto
		{
			Sistema = sistema,
			CdEmp = cdEmp,
			CdFil = cdFil,
			SqPesCorretor = sqPesCorretor
		});
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public void SistemaInteiro_DeveSerValido(int sistema)
	{
		Assert.Null(Validar(sistema));
	}

	[Fact]
	public void EmpresaFilialECorretor_DevemSerValidos()
	{
		Assert.Null(Validar(2, cdEmp: 2));
		Assert.Null(Validar(2, cdEmp: 2, cdFil: 1));
		Assert.Null(Validar(2, cdEmp: 2, sqPesCorretor: 12345));
		Assert.Null(Validar(2, cdEmp: 2, cdFil: 1, sqPesCorretor: 12345));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(9)]
	public void SistemaForaDaFaixa_DeveSerRecusado(int sistema)
	{
		Assert.Contains("Sistema inválido", Validar(sistema));
	}

	[Fact]
	public void FilialSemEmpresa_DeveSerRecusada()
	{
		Assert.Contains("informe também a empresa", Validar(2, cdFil: 1));
	}

	[Fact]
	public void CorretorSemEmpresa_DeveSerRecusado()
	{
		Assert.Contains("informe também a empresa", Validar(2, sqPesCorretor: 12345));
	}

	[Theory]
	[InlineData(0d)]
	[InlineData(100d)]
	[InlineData(1.5d)]
	public void EmpresaInvalida_DeveSerRecusada(double cdEmp)
	{
		Assert.Contains("empresa", Validar(2, cdEmp: (decimal)cdEmp));
	}

	[Theory]
	[InlineData(0d)]
	[InlineData(100d)]
	public void FilialInvalida_DeveSerRecusada(double cdFil)
	{
		Assert.Contains("filial", Validar(2, cdEmp: 2, cdFil: (decimal)cdFil));
	}

	[Theory]
	[InlineData(0d)]
	[InlineData(10_000_000d)]
	public void CorretorInvalido_DeveSerRecusado(double corretor)
	{
		Assert.Contains("corretor", Validar(2, cdEmp: 2, sqPesCorretor: (decimal)corretor));
	}
}
