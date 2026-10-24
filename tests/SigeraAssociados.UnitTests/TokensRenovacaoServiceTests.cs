using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Relógio controlado pelos testes.
/// </summary>
public sealed class RelogioFalso : TimeProvider
{
	public DateTimeOffset Agora { get; set; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

	public override DateTimeOffset GetUtcNow() => Agora;
}

/// <summary>
/// Testa a emissão, a renovação com rotação e a revogação
/// dos tokens de renovação (refresh tokens).
/// </summary>
public sealed class TokensRenovacaoServiceTests : IDisposable
{
	private const string UsuarioId = "usuario-1";

	private readonly SigeraIdentityDbContext _db;
	private readonly RelogioFalso _relogio = new();
	private readonly TokensRenovacaoService _servico;

	public TokensRenovacaoServiceTests()
	{
		_db = new SigeraIdentityDbContext(
			new DbContextOptionsBuilder<SigeraIdentityDbContext>()
				.UseInMemoryDatabase($"tokens-{Guid.NewGuid()}")
				.Options);

		_db.Users.Add(new IdentityUser
		{
			Id = UsuarioId,
			UserName = "u@teste.local",
			Email = "u@teste.local",
			EmailConfirmed = true
		});
		_db.SaveChanges();

		_servico = new TokensRenovacaoService(
			_db,
			_relogio,
			Options.Create(new OpcoesSessao { RefreshTokenDias = 7 }));
	}

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task Emitir_DeveGravarSomenteOHash()
	{
		var emitido = await _servico.EmitirAsync(UsuarioId);

		var registro = Assert.Single(_db.TokensRenovacao);
		Assert.NotEqual(emitido.Token, registro.TokenHash);
		Assert.Equal(TokensRenovacaoService.Hash(emitido.Token), registro.TokenHash);
		Assert.Equal(_relogio.Agora.AddDays(7), emitido.ExpiraEm);
	}

	[Fact]
	public async Task Renovar_TokenValido_DeveTrocarPorUmNovo()
	{
		var emitido = await _servico.EmitirAsync(UsuarioId);

		var resultado = await _servico.RenovarAsync(emitido.Token);

		Assert.Equal(StatusRenovacao.Sucesso, resultado.Status);
		Assert.Equal(UsuarioId, resultado.UsuarioId);
		Assert.NotEqual(emitido.Token, resultado.NovoToken!.Token);

		var antigo = _db.TokensRenovacao.Single(t => t.TokenHash == TokensRenovacaoService.Hash(emitido.Token));
		Assert.NotNull(antigo.RevogadoEmUtc);
		Assert.Equal(MotivosRevogacao.Renovado, antigo.MotivoRevogacao);
		Assert.Equal(TokensRenovacaoService.Hash(resultado.NovoToken.Token), antigo.SubstituidoPorHash);
	}

	[Fact]
	public async Task Renovar_TokenJaTrocado_DeveDetectarReusoERevogarTudo()
	{
		var original = await _servico.EmitirAsync(UsuarioId);
		var renovado = await _servico.RenovarAsync(original.Token);

		var reuso = await _servico.RenovarAsync(original.Token);

		Assert.Equal(StatusRenovacao.Reutilizado, reuso.Status);
		Assert.All(_db.TokensRenovacao, t => Assert.NotNull(t.RevogadoEmUtc));
		Assert.Equal(StatusRenovacao.Invalido, (await _servico.RenovarAsync(renovado.NovoToken!.Token)).Status);
	}

	[Fact]
	public async Task Renovar_TokenExpirado_DeveSerRecusado()
	{
		var emitido = await _servico.EmitirAsync(UsuarioId);
		_relogio.Agora = _relogio.Agora.AddDays(8);

		var resultado = await _servico.RenovarAsync(emitido.Token);

		Assert.Equal(StatusRenovacao.Expirado, resultado.Status);
	}

	[Theory]
	[InlineData("")]
	[InlineData("token-que-nao-existe")]
	public async Task Renovar_TokenDesconhecido_DeveSerInvalido(string token)
	{
		Assert.Equal(StatusRenovacao.Invalido, (await _servico.RenovarAsync(token)).Status);
	}

	[Fact]
	public async Task Renovar_UsuarioBloqueado_DeveSerRecusado()
	{
		var emitido = await _servico.EmitirAsync(UsuarioId);

		var usuario = _db.Users.Single();
		usuario.LockoutEnd = DateTimeOffset.MaxValue;
		await _db.SaveChangesAsync();

		Assert.Equal(StatusRenovacao.UsuarioIndisponivel, (await _servico.RenovarAsync(emitido.Token)).Status);
	}

	[Fact]
	public async Task Revogar_PorLogout_DeveImpedirRenovacaoSemTratarComoReuso()
	{
		var emitido = await _servico.EmitirAsync(UsuarioId);

		Assert.True(await _servico.RevogarAsync(emitido.Token, UsuarioId));

		var resultado = await _servico.RenovarAsync(emitido.Token);

		Assert.Equal(StatusRenovacao.Invalido, resultado.Status);
	}

	[Fact]
	public async Task Revogar_TokenDeOutroUsuario_NaoDeveRevogar()
	{
		var emitido = await _servico.EmitirAsync(UsuarioId);

		Assert.False(await _servico.RevogarAsync(emitido.Token, "outro-usuario"));
	}

	[Fact]
	public async Task RevogarTodos_DeveRevogarSomenteOsAtivos()
	{
		await _servico.EmitirAsync(UsuarioId);
		await _servico.EmitirAsync(UsuarioId);

		Assert.Equal(2, await _servico.RevogarTodosAsync(UsuarioId, MotivosRevogacao.Bloqueio));
		Assert.Equal(0, await _servico.RevogarTodosAsync(UsuarioId, MotivosRevogacao.Bloqueio));
	}
}
