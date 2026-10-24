using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;

namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Confere, a cada requisição autenticada, se a sessão ainda vale.
/// O carimbo de segurança e o bloqueio do usuário ficam em cache por
/// 1 minuto; alterações feitas pela própria API limpam o cache na hora.
/// Os tokens encerrados por logout ficam em cache até expirarem.
/// </summary>
public sealed class SessoesService : ISessoesService
{
	internal static readonly TimeSpan DuracaoCache = TimeSpan.FromMinutes(1);

	private readonly SigeraIdentityDbContext _identityDb;
	private readonly IMemoryCache _cache;
	private readonly TimeProvider _relogio;

	public SessoesService(
		SigeraIdentityDbContext identityDb,
		IMemoryCache cache,
		TimeProvider relogio)
	{
		_identityDb = identityDb;
		_cache = cache;
		_relogio = relogio;
	}

	internal static string ChaveUsuario(string usuarioId) => $"sigera:sessao:{usuarioId}";

	internal static string ChaveTokenEncerrado(string tokenId) => $"sigera:token-encerrado:{tokenId}";

	/// <inheritdoc />
	public async Task<bool> SessaoValidaAsync(
		string usuarioId,
		string? carimboSeguranca,
		string? tokenId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(usuarioId) || string.IsNullOrWhiteSpace(carimboSeguranca))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(tokenId)
			&& _cache.TryGetValue(ChaveTokenEncerrado(tokenId), out _))
		{
			return false;
		}

		var chave = ChaveUsuario(usuarioId);

		if (!_cache.TryGetValue(chave, out EstadoUsuario? estado) || estado is null)
		{
			estado = await _identityDb.Users
				.AsNoTracking()
				.Where(u => u.Id == usuarioId)
				.Select(u => new EstadoUsuario(u.SecurityStamp, u.LockoutEnd))
				.FirstOrDefaultAsync(cancellationToken)
				?? EstadoUsuario.Inexistente;

			_cache.Set(chave, estado, DuracaoCache);
		}

		if (ReferenceEquals(estado, EstadoUsuario.Inexistente))
		{
			return false;
		}

		var bloqueado = estado.BloqueadoAte is not null
			&& estado.BloqueadoAte > _relogio.GetUtcNow();

		return !bloqueado
			&& string.Equals(estado.CarimboSeguranca, carimboSeguranca, StringComparison.Ordinal);
	}

	/// <inheritdoc />
	public void EncerrarTokenAcesso(string tokenId, DateTimeOffset expiraEm)
	{
		if (string.IsNullOrWhiteSpace(tokenId))
		{
			return;
		}

		var restante = expiraEm - _relogio.GetUtcNow();

		if (restante > TimeSpan.Zero)
		{
			_cache.Set(ChaveTokenEncerrado(tokenId), true, restante + TimeSpan.FromMinutes(1));
		}
	}

	/// <inheritdoc />
	public void InvalidarCache(string usuarioId)
	{
		_cache.Remove(ChaveUsuario(usuarioId));
	}

	internal sealed record EstadoUsuario(string? CarimboSeguranca, DateTimeOffset? BloqueadoAte)
	{
		public static readonly EstadoUsuario Inexistente = new(null, null);
	}
}
