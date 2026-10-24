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
