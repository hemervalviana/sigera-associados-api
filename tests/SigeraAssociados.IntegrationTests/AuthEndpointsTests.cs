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

	// ----- Redefinição de senha pelo link do e-mail -----

	[Fact]
	public async Task FormularioRedefinicao_SemParametros_DeveMostrarLinkInvalido()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.GetAsync("/api/auth/redefinir-senha");
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
		Assert.Equal("text/html", resposta.Content.Headers.ContentType?.MediaType);
		Assert.Contains("Link inv", conteudo);
	}

	[Fact]
	public async Task FormularioRedefinicao_ComTokenMalformado_DeveMostrarLinkInvalido()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.GetAsync(
			"/api/auth/redefinir-senha?usuarioId=qualquer&token=!!!");

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	[Fact]
	public async Task EnvioDoFormulario_ComSenhasDiferentes_DeveMostrarOFormularioComErro()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsync(
			"/api/auth/redefinir-senha/formulario",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["usuarioId"] = "qualquer",
				["token"] = "dG9rZW4",
				["novaSenha"] = "SenhaForte#2026",
				["confirmarNovaSenha"] = "OutraSenha#2026"
			}));
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
		Assert.Contains("coincidem", conteudo);
		Assert.Contains("name=\"novaSenha\"", conteudo);
	}

	[Fact]
	public async Task EnvioDoFormulario_ComSenhaCurta_DeveMostrarOFormularioComErro()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsync(
			"/api/auth/redefinir-senha/formulario",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["usuarioId"] = "qualquer",
				["token"] = "dG9rZW4",
				["novaSenha"] = "Curta#1",
				["confirmarNovaSenha"] = "Curta#1"
			}));
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
		Assert.Contains("12 e 128", conteudo);
	}
}
