using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a concessão de acesso: opção todos os sistemas, empresa obtida
/// da base e conferência de filial e corretor.
/// O banco de negócio é simulado por um verificador falso.
/// </summary>
public sealed class ConcessaoAcessoTests : IDisposable
{
	/// <summary>
	/// Cadastro simulado: a base tem a empresa 2, as filiais 1 e 3 e o
	/// corretor 900 (da filial 1).
	/// </summary>
	private sealed class VerificadorFalso : IVerificadorCadastroSistema
	{
		public int Consultas { get; private set; }

		/// <summary>
		/// Empresa devolvida pela base. Null simula T009EMPS sem
		/// exatamente uma empresa.
		/// </summary>
		public decimal? Empresa { get; set; } = 2;

		public Task<decimal?> ObterEmpresaAsync(int sistema, CancellationToken cancellationToken = default)
		{
			Consultas++;
			return Task.FromResult(Empresa);
		}

		public Task<bool> FilialExisteAsync(int sistema, decimal cdEmp, decimal cdFil, CancellationToken cancellationToken = default)
		{
			Consultas++;
			return Task.FromResult(cdEmp == 2 && (cdFil == 1 || cdFil == 3));
		}

		public Task<bool> CorretorExisteAsync(int sistema, decimal cdEmp, decimal sqPesCorretor, decimal? cdFil, CancellationToken cancellationToken = default)
		{
			Consultas++;
			return Task.FromResult(cdEmp == 2 && sqPesCorretor == 900 && (cdFil is null || cdFil == 1));
		}
	}

	private const string UsuarioId = "usuario-1";

	private readonly SigeraIdentityDbContext _db;
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly VerificadorFalso _verificador = new();
	private readonly GestaoAcessosService _servico;

	public ConcessaoAcessoTests()
	{
		var opcoes = new DbContextOptionsBuilder<SigeraIdentityDbContext>()
			.UseInMemoryDatabase($"acessos-{Guid.NewGuid()}")
			.Options;

		_db = new SigeraIdentityDbContext(opcoes);
		_db.Users.Add(new IdentityUser { Id = UsuarioId, UserName = "u@teste.local", Email = "u@teste.local" });
		_db.SaveChanges();

		_servico = new GestaoAcessosService(_db, _cache, _verificador);
	}

	public void Dispose()
	{
		_db.Dispose();
		_cache.Dispose();
	}

	private Task<ResultadoGestaoAcesso> Conceder(
		int sistema = 2, decimal? cdFil = null, decimal? corretor = null) =>
		_servico.ConcederAsync(UsuarioId, new NovoAcessoDto
		{
			Sistema = sistema,
			CdFil = cdFil,
			SqPesCorretor = corretor
		});

	[Fact]
	public async Task SistemaInteiro_NaoPrecisaConferirCadastro()
	{
		var resultado = await Conceder();

		Assert.Equal(StatusGestaoAcesso.Sucesso, resultado.Status);
		Assert.Equal(0, _verificador.Consultas);
		Assert.Null(Assert.Single(_db.UsuarioAcessos).CdEmp);
	}

	[Fact]
	public async Task FilialECorretor_DevemGravarAEmpresaDaBase()
	{
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(cdFil: 1)).Status);
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(corretor: 900)).Status);
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(cdFil: 1, corretor: 900)).Status);

		Assert.All(_db.UsuarioAcessos, a => Assert.Equal(2m, a.CdEmp));
	}

	[Fact]
	public async Task BaseSemEmpresaUnica_DeveSerRecusada()
	{
		_verificador.Empresa = null;

		var resultado = await Conceder(cdFil: 1);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("T009EMPS", resultado.Mensagem);
		Assert.Contains("SONASP", resultado.Mensagem);
	}

	[Fact]
	public async Task FilialSemAssociados_DeveSerRecusada()
	{
		var resultado = await Conceder(cdFil: 9);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("filial 9", resultado.Mensagem);
	}

	[Fact]
	public async Task CorretorInexistente_DeveSerRecusado()
	{
		var resultado = await Conceder(corretor: 12345);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("corretor 12345", resultado.Mensagem);
	}

	[Fact]
	public async Task CorretorDeOutraFilial_DeveSerRecusado()
	{
		var resultado = await Conceder(cdFil: 3, corretor: 900);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("corretor 900", resultado.Mensagem);
		Assert.Contains("filial 3", resultado.Mensagem);
	}

	[Fact]
	public async Task AcessoRecusado_NaoDeveSerGravado()
	{
		await Conceder(cdFil: 9);

		Assert.Empty(_db.UsuarioAcessos);
	}

	[Fact]
	public async Task AcessoDuplicado_NaoConsultaOBancoDoSistema()
	{
		await Conceder(cdFil: 1);
		var consultasAntes = _verificador.Consultas;

		var resultado = await Conceder(cdFil: 1);

		Assert.Equal(StatusGestaoAcesso.Duplicado, resultado.Status);
		Assert.Equal(consultasAntes, _verificador.Consultas);
	}

	[Fact]
	public async Task Todos_DeveGravarUmaLinhaPorSistema()
	{
		var resultado = await Conceder(sistema: 0);

		Assert.Equal(StatusGestaoAcesso.Sucesso, resultado.Status);
		Assert.Equal(new[] { 1, 2, 3, 4 }, resultado.Acessos!.Select(a => a.Sistema));
		Assert.Equal(
			new[] { 1, 2, 3, 4 },
			_db.UsuarioAcessos.Select(a => a.Sistema).OrderBy(s => s).ToArray());
		Assert.All(_db.UsuarioAcessos, a => Assert.Null(a.CdEmp));
		Assert.Equal(0, _verificador.Consultas);
	}

	[Fact]
	public async Task Todos_ComAlgunsSistemasJaLiberados_DeveGravarSoOsQueFaltam()
	{
		await Conceder(sistema: 2);

		var resultado = await Conceder(sistema: 0);

		Assert.Equal(StatusGestaoAcesso.Sucesso, resultado.Status);
		Assert.Equal(new[] { 1, 3, 4 }, resultado.Acessos!.Select(a => a.Sistema));
		Assert.Equal(4, _db.UsuarioAcessos.Count());
	}

	[Fact]
	public async Task Todos_ComTodosJaLiberados_DeveSerDuplicado()
	{
		await Conceder(sistema: 0);

		var resultado = await Conceder(sistema: 0);

		Assert.Equal(StatusGestaoAcesso.Duplicado, resultado.Status);
	}

	[Fact]
	public async Task FilialNumSistema_NaoContaComoSistemaInteiroNaOpcaoTodos()
	{
		await Conceder(sistema: 2, cdFil: 1);

		var resultado = await Conceder(sistema: 0);

		Assert.Equal(4, resultado.Acessos!.Count);
	}
}
