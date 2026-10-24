using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa o formato único das respostas de erro (ProblemDetails):
/// content-type application/problem+json, título em português,
/// codigo, instance e traceId, e o tratamento de falhas inesperadas.
/// </summary>
public sealed class RespostasErroTests : IClassFixture<SigeraApiFactoryComServicosFalsos>
{
	private readonly SigeraApiFactoryComServicosFalsos _factory;

	public RespostasErroTests(SigeraApiFactoryComServicosFalsos factory)
	{
		_factory = factory;
		_factory.GestaoUsuarios.ExcecaoAoListar = null;
	}

	private static async Task<JsonElement> LerProblemaAsync(
		HttpResponseMessage resposta,
		HttpStatusCode statusEsperado)
	{
		Assert.Equal(statusEsperado, resposta.StatusCode);
		Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

		using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
		var raiz = documento.RootElement.Clone();

		Assert.Equal((int)statusEsperado, raiz.GetProperty("status").GetInt32());
		Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("title").GetString()));
		Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("codigo").GetString()));
		Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("traceId").GetString()));

		return raiz;
	}

	[Fact]
	public async Task ParametroInvalido_DeveRetornarProblemDetails400()
	{
		using var cliente = _factory.CreateClient();
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.GetAsync("/api/associados/consulta?pagina=0");
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.BadRequest);

		Assert.Equal("Requisição inválida", problema.GetProperty("title").GetString());
		Assert.Equal("parametro_invalido", problema.GetProperty("codigo").GetString());
		Assert.Equal("/api/associados/consulta", problema.GetProperty("instance").GetString());
		Assert.Contains("página", problema.GetProperty("detail").GetString());
	}

	[Fact]
	public async Task ValidacaoAutomatica_DeveTerTituloEmPortugues()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email = "", senha = "" });
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.BadRequest);

		Assert.Equal("Um ou mais campos são inválidos", problema.GetProperty("title").GetString());
		Assert.Equal("validacao", problema.GetProperty("codigo").GetString());
		Assert.True(problema.TryGetProperty("errors", out _));
	}

	[Fact]
	public async Task SemToken_DeveRetornarProblemDetails401()
	{
		using var cliente = _factory.CreateClient();
		cliente.DefaultRequestHeaders.Add(TesteAutenticacaoHandler.CabecalhoAnonimo, "true");

		using var resposta = await cliente.GetAsync("/api/conta");
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.Unauthorized);

		Assert.Equal("Não autenticado", problema.GetProperty("title").GetString());
	}

	[Fact]
	public async Task SemPermissao_DeveRetornarProblemDetails403()
	{
		using var cliente = _factory.CreateClient();
		cliente.DefaultRequestHeaders.Add(TesteAutenticacaoHandler.CabecalhoGrupos, "Corretores");

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios");
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.Forbidden);

		Assert.Equal("Acesso negado", problema.GetProperty("title").GetString());
	}

	[Fact]
	public async Task RotaInexistente_DeveRetornarProblemDetails404()
	{
		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.GetAsync("/api/rota-que-nao-existe");

		await LerProblemaAsync(resposta, HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task SistemaInvalido_DeveRetornarProblemDetailsComCodigo()
	{
		using var cliente = _factory.CreateClient();
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "9");

		using var resposta = await cliente.GetAsync("/api/associados");
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.BadRequest);

		Assert.Equal("sistema_invalido", problema.GetProperty("codigo").GetString());
	}

	[Fact]
	public async Task FalhaInesperada_DeveRetornar500SemDetalhesInternos()
	{
		_factory.GestaoUsuarios.ExcecaoAoListar =
			new InvalidOperationException("detalhe interno secreto");

		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios");
		var conteudo = await resposta.Content.ReadAsStringAsync();
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.InternalServerError);

		Assert.Equal("erro_interno", problema.GetProperty("codigo").GetString());
		Assert.DoesNotContain("detalhe interno secreto", conteudo);
	}

	[Fact]
	public async Task ErroDeNegocio_DeveRetornarProblemDetails()
	{
		_factory.GestaoUsuarios.ProximoResultado = new SigeraAssociados.Application.Usuarios.ResultadoGestaoUsuario(
			SigeraAssociados.Application.Usuarios.StatusGestaoUsuario.Proibido,
			"Não é permitido alterar a própria conta.");

		using var cliente = _factory.CreateClient();

		using var resposta = await cliente.PostAsync("/api/administracao/usuarios/qualquer/bloquear", null);
		var problema = await LerProblemaAsync(resposta, HttpStatusCode.Forbidden);

		Assert.Equal("Não é permitido alterar a própria conta.", problema.GetProperty("detail").GetString());

		_factory.GestaoUsuarios.ProximoResultado = GestaoUsuariosFalso.Sucesso();
	}
}
