using System.Net;
using System.Text.Json;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa o health check de vida (/health), que não depende dos bancos.
/// </summary>
public sealed class SaudeTests : IClassFixture<SigeraApiFactory>
{
	private readonly SigeraApiFactory _factory;

	public SaudeTests(SigeraApiFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task Health_DeveResponderSemTokenESemBanco()
	{
		using var cliente = _factory.CreateClient();
		cliente.DefaultRequestHeaders.Add(TesteAutenticacaoHandler.CabecalhoAnonimo, "true");

		using var resposta = await cliente.GetAsync("/health");
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

		using var documento = JsonDocument.Parse(conteudo);
		Assert.Equal("Healthy", documento.RootElement.GetProperty("status").GetString());
		Assert.Equal(0, documento.RootElement.GetProperty("verificacoes").GetArrayLength());
	}
}
