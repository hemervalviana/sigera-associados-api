using SigeraAssociados.Application.Erros;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Usuarios;
using SigeraAssociados.Application.Usuarios;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using SigeraAssociados.Infrastructure.Usuarios;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras da gestão de usuários com o Identity real
/// sobre um banco em memória (provedor InMemory).
/// </summary>
public sealed class GestaoUsuariosServiceTests : IAsyncLifetime
{
	private sealed class UsuarioAtualFalso : IUsuarioAtual
	{
		public string? UsuarioId { get; set; }

		public bool PossuiAcessoIrrestrito { get; set; }
	}

	private readonly UsuarioAtualFalso _executor = new();
	private ServiceProvider _provider = null!;
	private IServiceScope _escopo = null!;
	private UserManager<IdentityUser> _userManager = null!;
	private IGestaoUsuariosService _servico = null!;

	private string _adminId = string.Empty;
	private string _gestorId = string.Empty;
	private string _corretorId = string.Empty;

	public async Task InitializeAsync()
	{
		var services = new ServiceCollection();

		services.AddLogging();
		services.AddMemoryCache();

		var nomeBanco = $"identidade-{Guid.NewGuid()}";
		services.AddDbContext<SigeraIdentityDbContext>(
			o => o.UseInMemoryDatabase(nomeBanco));

		services.AddIdentityCore<IdentityUser>()
			.AddRoles<IdentityRole>()
			.AddEntityFrameworkStores<SigeraIdentityDbContext>();

		services.AddSingleton<IUsuarioAtual>(_executor);
		services.AddScoped<IGestaoUsuariosService, GestaoUsuariosService>();
		services.AddSingleton<TimeProvider>(TimeProvider.System);
		services.AddOptions<OpcoesSessao>();
		services.AddScoped<ITokensRenovacaoService, TokensRenovacaoService>();
		services.AddScoped<ISessoesService, SessoesService>();

		_provider = services.BuildServiceProvider();
		_escopo = _provider.CreateScope();

		_userManager = _escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
		var roleManager = _escopo.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

		foreach (var grupo in Grupos.Funcionais)
		{
			await roleManager.CreateAsync(new IdentityRole(grupo));
		}

		_adminId = await CriarAsync("admin@teste.local", Grupos.Administradores);
		_gestorId = await CriarAsync("gestor@teste.local", Grupos.Gestores);
		_corretorId = await CriarAsync("corretor@teste.local", Grupos.Corretores);

		_servico = _escopo.ServiceProvider.GetRequiredService<IGestaoUsuariosService>();
	}

	public async Task DisposeAsync()
	{
		_escopo.Dispose();
		await _provider.DisposeAsync();
	}

	private async Task<string> CriarAsync(string email, string grupo)
	{
		var usuario = new IdentityUser { UserName = email, Email = email };

		Assert.True((await _userManager.CreateAsync(usuario)).Succeeded);
		Assert.True((await _userManager.AddToRoleAsync(usuario, grupo)).Succeeded);

		return usuario.Id;
	}

	private void ComoAdministrador()
	{
		_executor.UsuarioId = _adminId;
		_executor.PossuiAcessoIrrestrito = true;
	}

	private void ComoGestor()
	{
		_executor.UsuarioId = _gestorId;
		_executor.PossuiAcessoIrrestrito = false;
	}

	[Fact]
	public async Task Listar_SemFiltro_DeveTrazerTodosComGrupos()
	{
		ComoAdministrador();

		var resultado = await _servico.ListarAsync(new ConsultaUsuariosRequest());

		Assert.Equal(3, resultado.TotalRegistros);
		Assert.Equal(1, resultado.TotalPaginas);

		var corretor = Assert.Single(resultado.Usuarios, u => u.Id == _corretorId);
		Assert.Equal(new[] { Grupos.Corretores }, corretor.Grupos);
	}

	[Fact]
	public async Task Listar_FiltrandoPorGrupo_DeveTrazerSomenteODoGrupo()
	{
		ComoAdministrador();

		var resultado = await _servico.ListarAsync(
			new ConsultaUsuariosRequest { Grupo = "gestores" });

		var usuario = Assert.Single(resultado.Usuarios);
		Assert.Equal(_gestorId, usuario.Id);
	}

	[Fact]
	public async Task Listar_FiltrandoPorEmail_DeveTrazerSomenteOCorrespondente()
	{
		ComoAdministrador();

		var resultado = await _servico.ListarAsync(
			new ConsultaUsuariosRequest { Busca = "corretor" });

		Assert.Equal(_corretorId, Assert.Single(resultado.Usuarios).Id);
	}

	[Fact]
	public async Task Listar_ComGrupoInvalido_DeveLancarArgumentException()
	{
		ComoAdministrador();

		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarAsync(new ConsultaUsuariosRequest { Grupo = "Diretoria" }));
	}

	[Theory]
	[InlineData(0, 20)]
	[InlineData(1, 0)]
	[InlineData(1, 101)]
	public async Task Listar_ComPaginacaoInvalida_DeveLancarArgumentException(int pagina, int tamanho)
	{
		ComoAdministrador();

		await Assert.ThrowsAsync<DadosInvalidosException>(() =>
			_servico.ListarAsync(new ConsultaUsuariosRequest { Pagina = pagina, TamanhoPagina = tamanho }));
	}

	[Fact]
	public async Task AlterarGrupo_DeveSubstituirOGrupoAtual()
	{
		ComoAdministrador();

		var resultado = await _servico.AlterarGrupoAsync(_corretorId, "atendimento");

		Assert.Equal(StatusGestaoUsuario.Sucesso, resultado.Status);
		Assert.Equal(new[] { Grupos.Atendimento }, resultado.Usuario!.Grupos);
	}

	[Fact]
	public async Task AlterarGrupo_ParaAdministradores_DeveSerRecusado()
	{
		ComoAdministrador();

		var resultado = await _servico.AlterarGrupoAsync(_corretorId, Grupos.Administradores);

		Assert.Equal(StatusGestaoUsuario.Invalido, resultado.Status);
	}

	[Fact]
	public async Task AlterarGrupo_ParaGrupoInexistente_DeveSerRecusado()
	{
		ComoAdministrador();

		var resultado = await _servico.AlterarGrupoAsync(_corretorId, "Diretoria");

		Assert.Equal(StatusGestaoUsuario.Invalido, resultado.Status);
	}

	[Fact]
	public async Task AlterarGrupo_DaPropriaConta_DeveSerProibido()
	{
		ComoGestor();

		var resultado = await _servico.AlterarGrupoAsync(_gestorId, Grupos.Corretores);

		Assert.Equal(StatusGestaoUsuario.Proibido, resultado.Status);
	}

	[Fact]
	public async Task Gestor_AlterandoAdministrador_DeveSerProibido()
	{
		ComoGestor();

		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.AlterarGrupoAsync(_adminId, Grupos.Corretores)).Status);
		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.BloquearAsync(_adminId)).Status);
		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.ExcluirAsync(_adminId)).Status);
	}

	[Fact]
	public async Task Gestor_AlterandoCorretor_DevePermitir()
	{
		ComoGestor();

		var resultado = await _servico.AlterarGrupoAsync(_corretorId, Grupos.Financeiro);

		Assert.Equal(StatusGestaoUsuario.Sucesso, resultado.Status);
	}

	[Fact]
	public async Task BloquearEDesbloquear_DeveAlterarOEstadoDaConta()
	{
		ComoAdministrador();

		var bloqueio = await _servico.BloquearAsync(_corretorId);
		Assert.Equal(StatusGestaoUsuario.Sucesso, bloqueio.Status);
		Assert.True(bloqueio.Usuario!.Bloqueado);

		var usuario = await _userManager.FindByIdAsync(_corretorId);
		Assert.True(await _userManager.IsLockedOutAsync(usuario!));

		var desbloqueio = await _servico.DesbloquearAsync(_corretorId);
		Assert.Equal(StatusGestaoUsuario.Sucesso, desbloqueio.Status);
		Assert.False(desbloqueio.Usuario!.Bloqueado);
	}

	[Fact]
	public async Task Bloquear_PropriaConta_DeveSerProibido()
	{
		ComoAdministrador();

		var resultado = await _servico.BloquearAsync(_adminId);

		Assert.Equal(StatusGestaoUsuario.Proibido, resultado.Status);
	}

	[Fact]
	public async Task Excluir_DeveRemoverAConta()
	{
		ComoAdministrador();

		var resultado = await _servico.ExcluirAsync(_corretorId);

		Assert.Equal(StatusGestaoUsuario.Sucesso, resultado.Status);
		Assert.Null(await _servico.ObterAsync(_corretorId));
	}

	[Fact]
	public async Task Operacoes_ComUsuarioInexistente_DevemDevolverNaoEncontrado()
	{
		ComoAdministrador();

		Assert.Null(await _servico.ObterAsync("nao-existe"));
		Assert.Equal(StatusGestaoUsuario.UsuarioNaoEncontrado,
			(await _servico.BloquearAsync("nao-existe")).Status);
		Assert.Equal(StatusGestaoUsuario.UsuarioNaoEncontrado,
			(await _servico.ExcluirAsync("nao-existe")).Status);
	}

	// ----- Incluir e remover grupos -----

	[Fact]
	public async Task AdicionarGrupo_DeveManterOsGruposAtuais()
	{
		ComoGestor();

		var resultado = await _servico.AdicionarGrupoAsync(_corretorId, "financeiro");

		Assert.Equal(StatusGestaoUsuario.Sucesso, resultado.Status);
		Assert.Equal(new[] { Grupos.Corretores, Grupos.Financeiro }, resultado.Usuario!.Grupos);
	}

	[Fact]
	public async Task AdicionarGrupo_QueJaPossui_DeveDevolverConflito()
	{
		ComoAdministrador();

		var resultado = await _servico.AdicionarGrupoAsync(_corretorId, Grupos.Corretores);

		Assert.Equal(StatusGestaoUsuario.Conflito, resultado.Status);
	}

	[Theory]
	[InlineData(Grupos.Administradores)]
	[InlineData("Diretoria")]
	[InlineData("")]
	public async Task AdicionarGrupo_Invalido_DeveSerRecusado(string grupo)
	{
		ComoAdministrador();

		var resultado = await _servico.AdicionarGrupoAsync(_corretorId, grupo);

		Assert.Equal(StatusGestaoUsuario.Invalido, resultado.Status);
	}

	[Fact]
	public async Task AdicionarGrupo_DeveTrocarOCarimboDeSeguranca()
	{
		ComoAdministrador();
		var usuario = await _userManager.FindByIdAsync(_corretorId);
		var carimboAntes = await _userManager.GetSecurityStampAsync(usuario!);

		await _servico.AdicionarGrupoAsync(_corretorId, Grupos.Atendimento);

		Assert.NotEqual(carimboAntes, await _userManager.GetSecurityStampAsync(usuario!));
	}

	[Fact]
	public async Task RemoverGrupo_DeveTirarSomenteOGrupoInformado()
	{
		ComoAdministrador();
		await _servico.AdicionarGrupoAsync(_corretorId, Grupos.Financeiro);

		var resultado = await _servico.RemoverGrupoAsync(_corretorId, "corretores");

		Assert.Equal(StatusGestaoUsuario.Sucesso, resultado.Status);
		Assert.Equal(new[] { Grupos.Financeiro }, resultado.Usuario!.Grupos);
	}

	[Fact]
	public async Task RemoverGrupo_UnicoGrupo_DeveSerRecusado()
	{
		ComoAdministrador();

		var resultado = await _servico.RemoverGrupoAsync(_corretorId, Grupos.Corretores);

		Assert.Equal(StatusGestaoUsuario.Invalido, resultado.Status);
		Assert.Equal(new[] { Grupos.Corretores }, (await _servico.ObterAsync(_corretorId))!.Grupos);
	}

	[Fact]
	public async Task RemoverGrupo_QueNaoPossui_DeveDevolverConflito()
	{
		ComoAdministrador();

		var resultado = await _servico.RemoverGrupoAsync(_corretorId, Grupos.Financeiro);

		Assert.Equal(StatusGestaoUsuario.Conflito, resultado.Status);
	}

	[Fact]
	public async Task RemoverGrupo_Inexistente_DeveSerRecusado()
	{
		ComoAdministrador();

		var resultado = await _servico.RemoverGrupoAsync(_corretorId, "Diretoria");

		Assert.Equal(StatusGestaoUsuario.Invalido, resultado.Status);
	}

	[Fact]
	public async Task Gestor_IncluindoOuRemovendoGrupoDeAdministrador_DeveSerProibido()
	{
		ComoGestor();

		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.AdicionarGrupoAsync(_adminId, Grupos.Financeiro)).Status);
		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.RemoverGrupoAsync(_adminId, Grupos.Administradores)).Status);
	}

	[Fact]
	public async Task IncluirOuRemoverGrupo_DaPropriaConta_DeveSerProibido()
	{
		ComoGestor();

		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.AdicionarGrupoAsync(_gestorId, Grupos.Financeiro)).Status);
		Assert.Equal(StatusGestaoUsuario.Proibido,
			(await _servico.RemoverGrupoAsync(_gestorId, Grupos.Gestores)).Status);
	}

	[Fact]
	public async Task AlterarGrupo_DeveTrocarOCarimboDeSeguranca()
	{
		ComoAdministrador();
		var usuario = await _userManager.FindByIdAsync(_corretorId);
		var carimboAntes = await _userManager.GetSecurityStampAsync(usuario!);

		await _servico.AlterarGrupoAsync(_corretorId, Grupos.Atendimento);

		Assert.NotEqual(carimboAntes, await _userManager.GetSecurityStampAsync(usuario!));
	}

	[Fact]
	public async Task Bloquear_DeveRevogarOsTokensDeRenovacao()
	{
		ComoAdministrador();
		var tokens = _escopo.ServiceProvider.GetRequiredService<ITokensRenovacaoService>();
		await tokens.EmitirAsync(_corretorId);

		await _servico.BloquearAsync(_corretorId);

		var db = _escopo.ServiceProvider.GetRequiredService<SigeraIdentityDbContext>();
		Assert.All(
			db.TokensRenovacao.Where(t => t.UsuarioId == _corretorId),
			t => Assert.Equal(MotivosRevogacao.Bloqueio, t.MotivoRevogacao));
	}
}
