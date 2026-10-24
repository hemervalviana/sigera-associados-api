using System.Net;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa o limite de 10 requisições por minuto nas rotas de autenticação.
/// Usa uma instância própria da API para não dividir o contador
/// com outros testes.
/// </summary>
public sealed class LimiteRequisicoesTests : IClassFixture<SigeraApiFactory>
{
	private readonly SigeraApiFactory _factory;

	public LimiteRequisicoesTests(SigeraApiFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task RotasDeAutenticacao_AposDezRequisicoes_DevemRetornar429()
	{
		using var cliente = _factory.CreateClient();

		// O GET de confirmação sem parâmetros responde 400 sem acessar o banco,
		// e está sob a mesma política do login.
		for (var i = 1; i <= 10; i++)
		{
			using var permitida = await cliente.GetAsync("/api/auth/confirmar-email");
			Assert.Equal(HttpStatusCode.BadRequest, permitida.StatusCode);
		}

		using var bloqueada = await cliente.GetAsync("/api/auth/confirmar-email");
		var conteudo = await bloqueada.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
		// O JSON escapa acentos (ç vira \u00E7), por isso o trecho sem acento.
		Assert.Contains("Muitas requisi", conteudo);
	}
}
