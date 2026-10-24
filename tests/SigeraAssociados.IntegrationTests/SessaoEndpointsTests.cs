using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa os endpoints de renovação de token e logout
/// nos casos que não dependem do banco de identidade.
/// </summary>
public sealed class SessaoEndpointsTests : IClassFixture<SigeraApiFactory>
{
	private readonly SigeraApiFactory _factory;

	public SessaoEndpointsTests(SigeraApiFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task Renovar_SemRefreshToken_DeveRetornar400()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = "" });

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	[Fact]
	public async Task Logout_SemToken_DeveRetornar401()
	{
		using var cliente = _factory.CreateClient();
		cliente.DefaultRequestHeaders.Add(TesteAutenticacaoHandler.CabecalhoAnonimo, "true");

		using var resposta = await cliente.PostAsJsonAsync("/api/auth/logout", new { });

		Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
	}

	[Fact]
	public async Task Logout_DaSessaoAtual_DeveRetornar200()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsJsonAsync("/api/auth/logout", new { todosOsDispositivos = false });
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
		Assert.Contains("Sess", conteudo);
	}
}
