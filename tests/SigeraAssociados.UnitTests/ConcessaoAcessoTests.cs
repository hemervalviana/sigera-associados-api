using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a conferência de empresa, filial e corretor ao conceder acesso.
/// O banco de negócio é simulado por um verificador falso.
/// </summary>
public sealed class ConcessaoAcessoTests : IDisposable
{
	/// <summary>
	/// Cadastro simulado: empresa 2, filial 1 e corretor 900 existem.
	/// </summary>
	private sealed class VerificadorFalso : IVerificadorCadastroSistema
	{
		public int Consultas { get; private set; }

		public Task<bool> EmpresaExisteAsync(int sistema, decimal cdEmp, CancellationToken cancellationToken = default)
		{
			Consultas++;
			return Task.FromResult(cdEmp == 2);
		}

		public Task<bool> FilialExisteAsync(int sistema, decimal cdEmp, decimal cdFil, CancellationToken cancellationToken = default)
		{
			Consultas++;
			return Task.FromResult(cdEmp == 2 && cdFil == 1);
		}

		public Task<bool> CorretorExisteAsync(int sistema, decimal cdEmp, decimal sqPesCorretor, CancellationToken cancellationToken = default)
		{
			Consultas++;
			return Task.FromResult(cdEmp == 2 && sqPesCorretor == 900);
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
		decimal? cdEmp = null, decimal? cdFil = null, decimal? corretor = null) =>
		_servico.ConcederAsync(UsuarioId, new NovoAcessoDto
		{
			Sistema = 2,
			CdEmp = cdEmp,
			CdFil = cdFil,
			SqPesCorretor = corretor
		});

	[Fact]
	public async Task SistemaInteiro_NaoPrecisaConferirCadastro()
	{
		var resultado = await Conceder();

		Assert.Equal(StatusGestaoAcesso.Sucesso, resultado.Status);
		Assert.Equal(0, _verificador.Consultas);
	}

	[Fact]
	public async Task EmpresaFilialECorretorExistentes_DevemSerConcedidos()
	{
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(cdEmp: 2)).Status);
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(cdEmp: 2, cdFil: 1)).Status);
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(cdEmp: 2, corretor: 900)).Status);
		Assert.Equal(StatusGestaoAcesso.Sucesso, (await Conceder(cdEmp: 2, cdFil: 1, corretor: 900)).Status);
	}

	[Fact]
	public async Task EmpresaInexistenteNoSistema_DeveSerRecusada()
	{
		var resultado = await Conceder(cdEmp: 5);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("empresa 5", resultado.Mensagem);
		Assert.Contains("SONASP", resultado.Mensagem);
	}

	[Fact]
	public async Task FilialSemAssociados_DeveSerRecusada()
	{
		var resultado = await Conceder(cdEmp: 2, cdFil: 9);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("filial 9", resultado.Mensagem);
	}

	[Fact]
	public async Task CorretorInexistente_DeveSerRecusado()
	{
		var resultado = await Conceder(cdEmp: 2, corretor: 12345);

		Assert.Equal(StatusGestaoAcesso.Invalido, resultado.Status);
		Assert.Contains("corretor 12345", resultado.Mensagem);
	}

	[Fact]
	public async Task AcessoRecusado_NaoDeveSerGravado()
	{
		await Conceder(cdEmp: 5);

		Assert.Empty(_db.UsuarioAcessos);
	}

	[Fact]
	public async Task AcessoDuplicado_NaoConsultaOBancoDoSistema()
	{
		await Conceder(cdEmp: 2);
		var consultasAntes = _verificador.Consultas;

		var resultado = await Conceder(cdEmp: 2);

		Assert.Equal(StatusGestaoAcesso.Duplicado, resultado.Status);
		Assert.Equal(consultasAntes, _verificador.Consultas);
	}
}
