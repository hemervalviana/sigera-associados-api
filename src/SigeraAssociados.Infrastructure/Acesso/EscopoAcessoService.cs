using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Resolve os escopos de acesso do usuário atual a partir da tabela
/// UsuarioAcessos do banco de identidade.
/// Os acessos não são gravados no token: são lidos do banco
/// e mantidos em cache por poucos minutos, para que uma revogação
/// tenha efeito sem esperar a expiração do token.
/// </summary>
public sealed class EscopoAcessoService : IEscopoAcessoService
{
	internal static readonly TimeSpan DuracaoCache = TimeSpan.FromMinutes(5);

	private readonly IUsuarioAtual _usuarioAtual;
	private readonly SigeraSystemContext _systemContext;
	private readonly SigeraIdentityDbContext _identityDb;
	private readonly IMemoryCache _cache;

	public EscopoAcessoService(
		IUsuarioAtual usuarioAtual,
		SigeraSystemContext systemContext,
		SigeraIdentityDbContext identityDb,
		IMemoryCache cache)
	{
		_usuarioAtual = usuarioAtual;
		_systemContext = systemContext;
		_identityDb = identityDb;
		_cache = cache;
	}

	internal static string ChaveCache(string usuarioId) =>
		$"sigera:acessos:{usuarioId}";

	/// <inheritdoc />
	public async Task<bool> PossuiAcessoAoSistemaAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		if (_usuarioAtual.PossuiAcessoIrrestrito)
		{
			return true;
		}

		var acessos = await ObterAcessosDoUsuarioAsync(cancellationToken);

		return acessos.Any(a => a.Sistema == sistema);
	}

	/// <inheritdoc />
	public async Task<EscopoConsulta> ObterEscopoAsync(
		CancellationToken cancellationToken = default)
	{
		if (_usuarioAtual.PossuiAcessoIrrestrito)
		{
			return EscopoConsulta.Total;
		}

		if (!_systemContext.SystemCode.HasValue)
		{
			throw new InvalidOperationException(
				"O sistema SIGERA não foi selecionado para esta requisição.");
		}

		var sistema = _systemContext.SystemCode.Value;
		var acessos = await ObterAcessosDoUsuarioAsync(cancellationToken);

		return EscopoConsulta.Criar(
			acessos
				.Where(a => a.Sistema == sistema)
				.Select(a => new EscopoAcesso(a.CdEmp, a.CdFil, a.SqPesCorretor)));
	}

	private async Task<IReadOnlyList<AcessoEmCache>> ObterAcessosDoUsuarioAsync(
		CancellationToken cancellationToken)
	{
		var usuarioId = _usuarioAtual.UsuarioId;

		if (string.IsNullOrWhiteSpace(usuarioId))
		{
			return Array.Empty<AcessoEmCache>();
		}

		var chave = ChaveCache(usuarioId);

		if (_cache.TryGetValue(chave, out IReadOnlyList<AcessoEmCache>? emCache)
			&& emCache is not null)
		{
			return emCache;
		}

		var acessos = await _identityDb.UsuarioAcessos
			.AsNoTracking()
			.Where(a => a.UsuarioId == usuarioId)
			.Select(a => new AcessoEmCache(
				a.Sistema,
				a.CdEmp,
				a.CdFil,
				a.SqPesCorretor))
			.ToListAsync(cancellationToken);

		_cache.Set(chave, (IReadOnlyList<AcessoEmCache>)acessos, DuracaoCache);

		return acessos;
	}

	internal sealed record AcessoEmCache(
		int Sistema,
		decimal? CdEmp,
		decimal? CdFil,
		decimal? SqPesCorretor);
}
