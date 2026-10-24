using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa os endpoints de autenticação que respondem sem acessar o banco.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<SigeraApiFactory>
{
	private readonly SigeraApiFactory _factory;

	public AuthEndpointsTests(SigeraApiFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task CadastroPublico_FoiRemovido_DeveRetornar404()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsJsonAsync("/api/auth/registro", new
		{
			email = "qualquer@teste.local",
			senha = "SenhaForte#2026",
			confirmarSenha = "SenhaForte#2026"
		});

		Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
	}

	[Fact]
	public async Task Login_SemEmail_DeveRetornar400()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new
		{
			email = "",
			senha = "qualquer"
		});

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	[Fact]
	public async Task ConfirmarEmailPorLink_SemParametros_DeveMostrarPaginaDeErro()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.GetAsync("/api/auth/confirmar-email");
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
		Assert.Equal("text/html", resposta.Content.Headers.ContentType?.MediaType);
		// O texto da página é codificado em HTML (á vira &#xE1;).
		Assert.Contains("Link inv", conteudo);
	}

	[Fact]
	public async Task ConfirmarEmailPorLink_ComTokenMalformado_DeveRetornar400()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.GetAsync(
			"/api/auth/confirmar-email?usuarioId=qualquer&token=!!!");

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}
}
