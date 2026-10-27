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

	// ----- Incluir e remover grupos -----

	[Fact]
	public async Task AdicionarGrupo_ComSucesso_DeveRetornar200()
	{
		using var cliente = Cliente("Gestores");

		using var resposta = await cliente.PostAsJsonAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos",
			new { grupo = "Financeiro" });

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
		Assert.Equal("Financeiro", _factory.GestaoUsuarios.UltimoGrupo);
	}

	[Fact]
	public async Task AdicionarGrupo_ComoCorretor_DeveRetornar403()
	{
		using var cliente = Cliente("Corretores");

		using var resposta = await cliente.PostAsJsonAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos",
			new { grupo = "Financeiro" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task AdicionarGrupo_SemGrupoNoCorpo_DeveRetornar400()
	{
		using var cliente = Cliente();

		using var resposta = await cliente.PostAsJsonAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos",
			new { grupo = "" });

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	[Fact]
	public async Task AdicionarGrupo_QuandoJaPertence_DeveRetornar409()
	{
		_factory.GestaoUsuarios.ProximoResultado =
			new ResultadoGestaoUsuario(StatusGestaoUsuario.Conflito, "Já pertence.");

		using var cliente = Cliente();

		using var resposta = await cliente.PostAsJsonAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos",
			new { grupo = "Corretores" });

		Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
	}

	[Fact]
	public async Task RemoverGrupo_ComNomeCodificadoNaUrl_DeveRetornar200()
	{
		using var cliente = Cliente("Gestores");

		using var resposta = await cliente.DeleteAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos/Coordenadora%20Adm.%20%26%20Operacional");

		Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
		Assert.Equal(Grupos.CoordenadoraAdmOperacional, _factory.GestaoUsuarios.UltimoGrupo);
	}

	[Fact]
	public async Task RemoverGrupo_ComoFinanceiro_DeveRetornar403()
	{
		using var cliente = Cliente("Financeiro");

		using var resposta = await cliente.DeleteAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos/Corretores");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task RemoverGrupo_QuandoNaoPertence_DeveRetornar409()
	{
		_factory.GestaoUsuarios.ProximoResultado =
			new ResultadoGestaoUsuario(StatusGestaoUsuario.Conflito, "Não pertence.");

		using var cliente = Cliente();

		using var resposta = await cliente.DeleteAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos/Financeiro");

		Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
	}

	[Fact]
	public async Task RemoverGrupo_UnicoGrupo_DeveRetornar400()
	{
		_factory.GestaoUsuarios.ProximoResultado =
			new ResultadoGestaoUsuario(StatusGestaoUsuario.Invalido, "Precisa de um grupo.");

		using var cliente = Cliente();

		using var resposta = await cliente.DeleteAsync(
			"/api/administracao/usuarios/usuario-alvo/grupos/Corretores");

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	// ----- Pesquisa de filiais e corretores -----

	[Theory]
	[InlineData("/api/administracao/sistemas/2/filiais")]
	[InlineData("/api/administracao/sistemas/2/corretores?cdFil=1")]
	public async Task CadastrosDoSistema_ComoGestor_DeveRetornar403(string rota)
	{
		using var cliente = Cliente("Gestores");

		using var resposta = await cliente.GetAsync(rota);

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("/api/administracao/sistemas/0/filiais")]
	[InlineData("/api/administracao/sistemas/9/filiais")]
	[InlineData("/api/administracao/sistemas/9/corretores")]
	[InlineData("/api/administracao/sistemas/2/corretores?cdFil=100")]
	[InlineData("/api/administracao/sistemas/2/corretores?tamanhoPagina=500")]
	public async Task CadastrosDoSistema_ComFiltroInvalido_DeveRetornar400(string rota)
	{
		using var cliente = Cliente("Administradores");

		using var resposta = await cliente.GetAsync(rota);

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
	}

	// ----- Cadastro de associados: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	[InlineData("Financeiro")]
	public async Task ExcluirAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Fact]
	public async Task IncluirAssociado_SemCabecalhoDoSistema_DeveRetornar400()
	{
		using var cliente = Cliente("Atendimento");

		using var resposta = await cliente.PostAsJsonAsync("/api/associados", new { nome = "MARIA" });

		Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
		Assert.Contains("X-Sigera-Sistema", await resposta.Content.ReadAsStringAsync());
	}

	// ----- Manutenção das tabelas de apoio: só administradores -----

	[Theory]
	[InlineData("Gestores", "/api/cadastros/filiais")]
	[InlineData("Atendimento", "/api/cadastros/situacoes")]
	[InlineData("Corretores", "/api/cadastros/tratamentos")]
	[InlineData("Financeiro", "/api/cadastros/municipios")]
	public async Task IncluirEmTabelaDeApoio_SemSerAdministrador_DeveRetornar403(string grupo, string rota)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.PostAsJsonAsync(rota, new { descricao = "TESTE", nome = "TESTE" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("/api/cadastros/filiais/1")]
	[InlineData("/api/cadastros/situacoes/1")]
	[InlineData("/api/cadastros/tratamentos/1")]
	[InlineData("/api/cadastros/municipios/2307304")]
	public async Task ExcluirDeTabelaDeApoio_ComoGestor_DeveRetornar403(string rota)
	{
		using var cliente = Cliente("Gestores");
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync(rota);

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Órgãos e vínculos: permissões -----

	[Theory]
	[InlineData("Gestores", "/api/orgaos")]
	[InlineData("Atendimento", "/api/orgaos/lotacoes")]
	[InlineData("Corretores", "/api/orgaos/situacoes")]
	[InlineData("Financeiro", "/api/orgaos/cargos")]
	[InlineData("Gestores", "/api/orgaos/grupos")]
	[InlineData("Atendimento", "/api/orgaos/suborgaos")]
	public async Task IncluirOrgaoOuApoio_SemSerAdministrador_DeveRetornar403(string grupo, string rota)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.PostAsJsonAsync(rota, new { nome = "TESTE", descricao = "TESTE" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	public async Task ExcluirVinculoDeOrgao_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/orgaos/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Endereços do associado e tipos de endereço: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	public async Task ExcluirEnderecoDoAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/enderecos/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("Gestores")]
	[InlineData("Atendimento")]
	public async Task IncluirTipoEndereco_SemSerAdministrador_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.PostAsJsonAsync("/api/cadastros/tipos-endereco", new { descricao = "TESTE" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Telefones do associado e tipos de telefone: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	public async Task ExcluirTelefoneDoAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/telefones/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("Gestores")]
	[InlineData("Atendimento")]
	public async Task IncluirTipoTelefone_SemSerAdministrador_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.PostAsJsonAsync("/api/cadastros/tipos-telefone", new { descricao = "TESTE" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Chaves Pix do associado: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	public async Task ExcluirChavePixDoAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/pix/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Referências do associado: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	public async Task ExcluirReferenciaDoAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/referencias/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Créditos do associado e cadastros financeiros: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Atendimento")]
	[InlineData("Financeiro")]
	public async Task ExcluirCreditoDoAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/creditos/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("Gestores", "/api/cadastros/formas-pagamento")]
	[InlineData("Financeiro", "/api/cadastros/formas-pagamento")]
	[InlineData("Gestores", "/api/cadastros/produtos")]
	[InlineData("Atendimento", "/api/cadastros/produtos")]
	[InlineData("Gestores", "/api/convenios")]
	[InlineData("Financeiro", "/api/convenios")]
	public async Task IncluirCadastroFinanceiro_SemSerAdministrador_DeveRetornar403(string grupo, string rota)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.PostAsJsonAsync(rota, new { descricao = "TESTE", nome = "TESTE" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	// ----- Contas bancárias e bancos: permissões -----

	[Theory]
	[InlineData("Corretores")]
	[InlineData("Financeiro")]
	public async Task ExcluirContaDoAssociado_SemSerAdministradorOuGestor_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.DeleteAsync("/api/associados/123/contas/1");

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
	}

	[Theory]
	[InlineData("Gestores")]
	[InlineData("Financeiro")]
	public async Task IncluirBanco_SemSerAdministrador_DeveRetornar403(string grupo)
	{
		using var cliente = Cliente(grupo);
		cliente.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");

		using var resposta = await cliente.PostAsJsonAsync("/api/cadastros/bancos", new { cdBanco = 999, nome = "TESTE" });

		Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
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
