using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a validação da sessão feita a cada requisição autenticada.
/// </summary>
public sealed class SessoesServiceTests : IDisposable
{
	private const string UsuarioId = "usuario-1";
	private const string Carimbo = "carimbo-atual";

	private readonly SigeraIdentityDbContext _db;
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly RelogioFalso _relogio = new();
	private readonly SessoesService _servico;

	public SessoesServiceTests()
	{
		_db = new SigeraIdentityDbContext(
			new DbContextOptionsBuilder<SigeraIdentityDbContext>()
				.UseInMemoryDatabase($"sessoes-{Guid.NewGuid()}")
				.Options);

		_db.Users.Add(new IdentityUser
		{
			Id = UsuarioId,
			UserName = "u@teste.local",
			SecurityStamp = Carimbo
		});
		_db.SaveChanges();

		_servico = new SessoesService(_db, _cache, _relogio);
	}

	public void Dispose()
	{
		_db.Dispose();
		_cache.Dispose();
	}

	[Fact]
	public async Task CarimboAtual_DeveValer()
	{
		Assert.True(await _servico.SessaoValidaAsync(UsuarioId, Carimbo, "token-1"));
	}

	[Theory]
	[InlineData("carimbo-antigo")]
	[InlineData("")]
	[InlineData(null)]
	public async Task CarimboDiferenteOuAusente_NaoDeveValer(string? carimbo)
	{
		Assert.False(await _servico.SessaoValidaAsync(UsuarioId, carimbo, "token-1"));
	}

	[Fact]
	public async Task UsuarioInexistente_NaoDeveValer()
	{
		Assert.False(await _servico.SessaoValidaAsync("nao-existe", Carimbo, "token-1"));
	}

	[Fact]
	public async Task UsuarioBloqueado_NaoDeveValer()
	{
		_db.Users.Single().LockoutEnd = DateTimeOffset.MaxValue;
		await _db.SaveChangesAsync();

		Assert.False(await _servico.SessaoValidaAsync(UsuarioId, Carimbo, "token-1"));
	}

	[Fact]
	public async Task TokenEncerradoPorLogout_NaoDeveValer()
	{
		_servico.EncerrarTokenAcesso("token-1", _relogio.Agora.AddMinutes(10));

		Assert.False(await _servico.SessaoValidaAsync(UsuarioId, Carimbo, "token-1"));
		Assert.True(await _servico.SessaoValidaAsync(UsuarioId, Carimbo, "token-2"));
	}

	[Fact]
	public async Task CarimboNovo_DeveValerDepoisDeInvalidarOCache()
	{
		Assert.True(await _servico.SessaoValidaAsync(UsuarioId, Carimbo, "token-1"));

		_db.Users.Single().SecurityStamp = "carimbo-novo";
		await _db.SaveChangesAsync();

		_servico.InvalidarCache(UsuarioId);

		Assert.False(await _servico.SessaoValidaAsync(UsuarioId, Carimbo, "token-1"));
		Assert.True(await _servico.SessaoValidaAsync(UsuarioId, "carimbo-novo", "token-3"));
	}
}
