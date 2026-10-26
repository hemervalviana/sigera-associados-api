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
		decimal? cdFil = null,
		decimal? sqPesCorretor = null)
	{
		return GestaoAcessosService.Validar(new NovoAcessoDto
		{
			Sistema = sistema,
			CdFil = cdFil,
			SqPesCorretor = sqPesCorretor
		});
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(4)]
	public void SistemaInteiroOuTodos_DeveSerValido(int sistema)
	{
		Assert.Null(Validar(sistema));
	}

	[Fact]
	public void FilialECorretor_SemEmpresa_DevemSerValidos()
	{
		Assert.Null(Validar(2, cdFil: 1));
		Assert.Null(Validar(2, sqPesCorretor: 12345));
		Assert.Null(Validar(2, cdFil: 1, sqPesCorretor: 12345));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(5)]
	[InlineData(9)]
	public void SistemaForaDaFaixa_DeveSerRecusado(int sistema)
	{
		Assert.Contains("Sistema inválido", Validar(sistema));
	}

	[Fact]
	public void Todos_ComFilial_DeveSerRecusado()
	{
		Assert.Contains("escolha um sistema", Validar(0, cdFil: 1));
	}

	[Fact]
	public void Todos_ComCorretor_DeveSerRecusado()
	{
		Assert.Contains("escolha um sistema", Validar(0, sqPesCorretor: 12345));
	}

	[Theory]
	[InlineData(0d)]
	[InlineData(100d)]
	public void FilialInvalida_DeveSerRecusada(double cdFil)
	{
		Assert.Contains("filial", Validar(2, cdFil: (decimal)cdFil));
	}

	[Theory]
	[InlineData(0d)]
	[InlineData(10_000_000d)]
	public void CorretorInvalido_DeveSerRecusado(double corretor)
	{
		Assert.Contains("corretor", Validar(2, sqPesCorretor: (decimal)corretor));
	}
}
