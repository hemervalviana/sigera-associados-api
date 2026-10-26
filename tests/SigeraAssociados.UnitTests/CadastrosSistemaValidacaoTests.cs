using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Infrastructure.Cadastros;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a validação dos filtros das tabelas de apoio.
/// A validação acontece antes de abrir o banco, por isso a fábrica é nula.
/// </summary>
public sealed class CadastrosSistemaValidacaoTests
{
	private readonly CadastrosSistemaService _servico = new(null!);

	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	public async Task SistemaInvalido_DeveSerRecusado(int sistema)
	{
		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarSituacoesAsync(sistema));
		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarFiliaisAsync(sistema));
	}

	[Theory]
	[InlineData("C")]
	[InlineData("CEA")]
	[InlineData("C1")]
	public async Task UfInvalida_DeveSerRecusada(string uf)
	{
		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarMunicipiosAsync(2, new ConsultaMunicipiosRequest { Uf = uf }));
	}

	[Theory]
	[InlineData(0, 50)]
	[InlineData(1, 0)]
	[InlineData(1, 201)]
	public async Task PaginacaoInvalida_DeveSerRecusada(int pagina, int tamanho)
	{
		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarMunicipiosAsync(2, new ConsultaMunicipiosRequest { Pagina = pagina, TamanhoPagina = tamanho }));
		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarCorretoresAsync(2, new ConsultaCorretoresRequest { Pagina = pagina, TamanhoPagina = tamanho }));
	}

	[Fact]
	public async Task FilialInvalidaNaPesquisaDeCorretores_DeveSerRecusada()
	{
		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarCorretoresAsync(2, new ConsultaCorretoresRequest { CdFil = 100 }));
	}
}
