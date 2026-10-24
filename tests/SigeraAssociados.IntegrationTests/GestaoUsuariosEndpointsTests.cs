using System.Net;
using System.Net.Http.Json;
using SigeraAssociados.Application.Usuarios;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa os endpoints de gestão de usuários, de conta e as permissões
/// por grupo. A gestão de usuários e de acessos é falsa: os testes
/// verificam autorização, validação e conversão dos resultados em HTTP.
/// </summary>
public sealed class GestaoUsuariosEndpointsTests
	: IClassFixture<SigeraApiFactoryComServicosFalsos>
{
	private readonly SigeraApiFactoryComServicosFalsos _factory;

	public GestaoUsuariosEndpointsTests(SigeraApiFactoryComServicosFalsos factory)
	{
		_factory = factory;
		_factory.GestaoUsuarios.ProximoResultado = GestaoUsuariosFalso.Sucesso();
	}

	private HttpClient Cliente(string? grupos = null, bool anonimo = false)
	{
		var cliente = _factory.CreateClient();

		if (grupos is not null)
		{
			cliente.DefaultRequestHeaders.Add(TesteAutenticacaoHandler.CabecalhoGrupos, grupos);
		}

		if (anonimo)
		{
			cliente.DefaultRequestHeaders.Add(TesteAutenticacaoHandler.CabecalhoAnonimo, "true");
		}

		return cliente;
	}

	// ----- Quem pode gerenciar usuários -----

	[Theory]
	[InlineData("Administradores")]
	[InlineData("Administrador")]
	[InlineData("Gestores")]
	public async Task ListarUsuarios_ComPerfilDeGestao_DeveRetornar200(string grupo)
	{
		using var cliente = Cliente(grupo);

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios");

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
	}

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Financeiro")]
	[InlineData("Atendimento")]
	[InlineData("Associados")]
	public async Task ListarUsuarios_SemPerfilDeGestao_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task ListarUsuarios_SemToken_DeveRetornar401()
	{
		using var cliente = Cliente(anonimo: true);

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios");

		Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
	}

	[Fact]
	public async Task CriarUsuario_ComoCorretor_DeveRetornar403()
	{
		using var cliente = Cliente("Corretores");

		using var resposta = await cliente.PostAsJsonAsync("/api/administracao/usuarios", new
		{
			email = "novo@teste.local",
			senha = "SenhaForte#2026",
			confirmarSenha = "SenhaForte#2026",
			grupo = "Corretores"
		});

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task CriarUsuario_NoGrupoAdministradores_DeveRetornar400()
	{
		using var cliente = Cliente("Gestores");

		using var resposta = await cliente.PostAsJsonAsync("/api/administracao/usuarios", new
		{
			email = "tentativa@teste.local",
			senha = "SenhaForte#2026",
			confirmarSenha = "SenhaForte#2026",
			grupo = "Administradores"
		});

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	// ----- Rotas exclusivas de administradores -----

	[Fact]
	public async Task Acessos_ComoGestor_DeveRetornar403()
	{
		using var cliente = Cliente("Gestores");

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios/usuario-alvo/acessos");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task Acessos_ComoAdministrador_DeveRetornar200()
	{
		using var cliente = Cliente("Administradores");

		using var resposta = await cliente.GetAsync("/api/administracao/usuarios/usuario-alvo/acessos");

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
	}

	[Fact]
	public async Task Diagnostico_ComoGestor_DeveRetornar403()
	{
		using var cliente = Cliente("Gestores");
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "1");

		using var resposta = await cliente.GetAsync("/api/diagnostico/conexao");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Conversão dos resultados em status HTTP -----

	[Fact]
	public async Task ObterUsuario_Inexistente_DeveRetornar404()
	{
		using var cliente = Cliente();

		using var resposta = await cliente.GetAsync(
			$"/api/administracao/usuarios/{GestaoUsuariosFalso.IdInexistente}");

		Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
	}

	[Fact]
	public async Task ObterUsuario_Existente_DeveRetornar200ComOsDados()
	{
		using var cliente = Cliente();

		var usuario = await cliente.GetFromJsonAsync<UsuarioResumoDto>(
			"/api/administracao/usuarios/usuario-alvo");

		Assert.Equal("usuario-alvo", usuario!.Id);
	}

	[Fact]
	public async Task Bloquear_ComSucesso_DeveRetornar200()
	{
		using var cliente = Cliente("Gestores");

		using var resposta = await cliente.PostAsync("/api/administracao/usuarios/usuario-alvo/bloquear", null);

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
	}

	[Fact]
	public async Task Bloquear_QuandoProibido_DeveRetornar403()
	{
		_factory.GestaoUsuarios.ProximoResultado =
			new ResultadoGestaoUsuario(StatusGestaoUsuario.Proibido, "Não é permitido.");

		using var cliente = Cliente();

		using var resposta = await cliente.PostAsync("/api/administracao/usuarios/usuario-alvo/bloquear", null);

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task Excluir_QuandoNaoEncontrado_DeveRetornar404()
	{
		_factory.GestaoUsuarios.ProximoResultado =
			new ResultadoGestaoUsuario(StatusGestaoUsuario.UsuarioNaoEncontrado, "Usuário não encontrado.");

		using var cliente = Cliente();

		using var resposta = await cliente.DeleteAsync("/api/administracao/usuarios/qualquer");

		Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
	}

	[Fact]
	public async Task AlterarGrupo_QuandoInvalido_DeveRetornar400()
	{
		_factory.GestaoUsuarios.ProximoResultado =
			new ResultadoGestaoUsuario(StatusGestaoUsuario.Invalido, "Grupo inválido.");

		using var cliente = Cliente();

		using var resposta = await cliente.PutAsJsonAsync(
			"/api/administracao/usuarios/usuario-alvo/grupo",
			new { grupo = "Diretoria" });

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	[Fact]
	public async Task AlterarGrupo_SemGrupoNoCorpo_DeveRetornar400()
	{
		using var cliente = Cliente();

		using var resposta = await cliente.PutAsJsonAsync(
			"/api/administracao/usuarios/usuario-alvo/grupo",
			new { grupo = "" });

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	// ----- Minha conta -----

	[Fact]
	public async Task Conta_ComoAdministrador_DeveInformarAcessoIrrestrito()
	{
		using var cliente = Cliente("Administradores");

		using var resposta = await cliente.GetAsync("/api/conta");
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
		Assert.Contains("\"acessoIrrestrito\":true", conteudo);
	}

	[Fact]
	public async Task Conta_ComoCorretor_NaoDeveTerAcessoIrrestrito()
	{
		using var cliente = Cliente("Corretores");

		using var resposta = await cliente.GetAsync("/api/conta");
		var conteudo = await resposta.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
		Assert.Contains("\"acessoIrrestrito\":false", conteudo);
	}

	[Fact]
	public async Task Conta_SemToken_DeveRetornar401()
	{
		using var cliente = Cliente(anonimo: true);

		using var resposta = await cliente.GetAsync("/api/conta");

		Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
	}
}
