using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Garante que o cabeçalho X-Sigera-Sistema só é exigido
/// nas rotas de negócio.
/// </summary>
public sealed class RotasSemSistemaTests : IClassFixture<SigeraApiFactory>
{
	private readonly SigeraApiFactory _factory;

	public RotasSemSistemaTests(SigeraApiFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task Administracao_SemCabecalhoDoSistema_NaoDeveExigirCabecalho()
	{
		using var client = _factory.CreateClient();

		// Sistema inválido: a validação do modelo responde 400
		// antes de qualquer acesso ao banco de identidade.
		using var response = await client.PostAsJsonAsync(
			"/api/administracao/usuarios/qualquer/acessos",
			new { sistema = 9 });

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

		var conteudo = await response.Content.ReadAsStringAsync();

		Assert.DoesNotContain("X-Sigera-Sistema", conteudo);
	}

	[Theory]
	[InlineData("/api/cadastros/filiais")]
	[InlineData("/api/cadastros/situacoes")]
	[InlineData("/api/cadastros/tratamentos")]
	[InlineData("/api/cadastros/municipios")]
	[InlineData("/api/orgaos")]
	[InlineData("/api/orgaos/lotacoes")]
	[InlineData("/api/orgaos/situacoes")]
	[InlineData("/api/orgaos/cargos")]
	[InlineData("/api/orgaos/grupos")]
	[InlineData("/api/orgaos/suborgaos")]
	[InlineData("/api/associados/1/orgaos")]
	public async Task TabelasDeApoio_SemCabecalhoDoSistema_DevemExigir(string rota)
	{
		using var client = _factory.CreateClient();

		using var response = await client.GetAsync(rota);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("X-Sigera-Sistema", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Associados_SemCabecalhoDoSistema_DeveContinuarExigindo()
	{
		using var client = _factory.CreateClient();

		using var response = await client.GetAsync("/api/associados");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

		var conteudo = await response.Content.ReadAsStringAsync();

		Assert.Contains("X-Sigera-Sistema", conteudo);
	}
}
