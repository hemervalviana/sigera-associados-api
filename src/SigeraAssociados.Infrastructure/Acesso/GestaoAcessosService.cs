using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Concede, lista e revoga escopos de acesso dos usuários.
/// Cada alteração invalida o cache de acessos do usuário afetado.
/// </summary>
public sealed class GestaoAcessosService : IGestaoAcessosService
{
	private readonly SigeraIdentityDbContext _identityDb;
	private readonly IMemoryCache _cache;

	public GestaoAcessosService(
		SigeraIdentityDbContext identityDb,
		IMemoryCache cache)
	{
		_identityDb = identityDb;
		_cache = cache;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<UsuarioAcessoDto>?> ListarAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		if (!await UsuarioExisteAsync(usuarioId, cancellationToken))
		{
			return null;
		}

		var acessos = await _identityDb.UsuarioAcessos
			.AsNoTracking()
			.Where(a => a.UsuarioId == usuarioId)
			.OrderBy(a => a.Sistema)
			.ThenBy(a => a.CdEmp)
			.ThenBy(a => a.CdFil)
			.ThenBy(a => a.SqPesCorretor)
			.ToListAsync(cancellationToken);

		return acessos.Select(ParaDto).ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoAcesso> ConcederAsync(
		string usuarioId,
		NovoAcessoDto acesso,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(acesso);

		var erro = Validar(acesso);

		if (erro is not null)
		{
			return new ResultadoGestaoAcesso(StatusGestaoAcesso.Invalido, erro);
		}

		if (!await UsuarioExisteAsync(usuarioId, cancellationToken))
		{
			return new ResultadoGestaoAcesso(
				StatusGestaoAcesso.UsuarioNaoEncontrado,
				"Usuário não encontrado.");
		}

		// O índice único do SQL Server ignora linhas com colunas nulas,
		// por isso a duplicidade também é verificada aqui.
		var duplicado = await _identityDb.UsuarioAcessos.AnyAsync(
			a => a.UsuarioId == usuarioId
				&& a.Sistema == acesso.Sistema
				&& a.CdEmp == acesso.CdEmp
				&& a.CdFil == acesso.CdFil
				&& a.SqPesCorretor == acesso.SqPesCorretor,
			cancellationToken);

		if (duplicado)
		{
			return new ResultadoGestaoAcesso(
				StatusGestaoAcesso.Duplicado,
				"Este acesso já está cadastrado para o usuário.");
		}

		var entidade = new UsuarioAcesso
		{
			UsuarioId = usuarioId,
			Sistema = acesso.Sistema,
			CdEmp = acesso.CdEmp,
			CdFil = acesso.CdFil,
			SqPesCorretor = acesso.SqPesCorretor,
			CriadoEmUtc = DateTime.UtcNow
		};

		_identityDb.UsuarioAcessos.Add(entidade);
		await _identityDb.SaveChangesAsync(cancellationToken);

		_cache.Remove(EscopoAcessoService.ChaveCache(usuarioId));

		return new ResultadoGestaoAcesso(
			StatusGestaoAcesso.Sucesso,
			Acesso: ParaDto(entidade));
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoAcesso> RevogarAsync(
		string usuarioId,
		int acessoId,
		CancellationToken cancellationToken = default)
	{
		var entidade = await _identityDb.UsuarioAcessos
			.FirstOrDefaultAsync(
				a => a.Id == acessoId && a.UsuarioId == usuarioId,
				cancellationToken);

		if (entidade is null)
		{
			return new ResultadoGestaoAcesso(
				StatusGestaoAcesso.AcessoNaoEncontrado,
				"Acesso não encontrado para o usuário informado.");
		}

		_identityDb.UsuarioAcessos.Remove(entidade);
		await _identityDb.SaveChangesAsync(cancellationToken);

		_cache.Remove(EscopoAcessoService.ChaveCache(usuarioId));

		return new ResultadoGestaoAcesso(
			StatusGestaoAcesso.Sucesso,
			Acesso: ParaDto(entidade));
	}

	/// <summary>
	/// Valida as regras de um novo escopo. Retorna a mensagem de erro
	/// ou null quando o escopo é válido.
	/// </summary>
	internal static string? Validar(NovoAcessoDto acesso)
	{
		if (!SigeraSystemCatalog.TryGetDatabase(acesso.Sistema, out _))
		{
			return "Sistema inválido. Os valores permitidos são 1, 2, 3 e 4.";
		}

		if (acesso.CdEmp is not null && (acesso.CdEmp < 1 || acesso.CdEmp > 99 || acesso.CdEmp % 1 != 0))
		{
			return "A empresa deve ser um código inteiro entre 1 e 99.";
		}

		if (acesso.CdFil is not null && (acesso.CdFil < 1 || acesso.CdFil > 99 || acesso.CdFil % 1 != 0))
		{
			return "A filial deve ser um código inteiro entre 1 e 99.";
		}

		if (acesso.SqPesCorretor is not null
			&& (acesso.SqPesCorretor < 1 || acesso.SqPesCorretor > 9_999_999 || acesso.SqPesCorretor % 1 != 0))
		{
			return "O corretor deve ser um código inteiro entre 1 e 9999999.";
		}

		if (acesso.CdFil is not null && acesso.CdEmp is null)
		{
			return "Para restringir por filial, informe também a empresa.";
		}

		if (acesso.SqPesCorretor is not null && acesso.CdEmp is null)
		{
			return "Para restringir pela carteira do corretor, informe também a empresa.";
		}

		return null;
	}

	private Task<bool> UsuarioExisteAsync(
		string usuarioId,
		CancellationToken cancellationToken)
	{
		return _identityDb.Users.AnyAsync(
			u => u.Id == usuarioId,
			cancellationToken);
	}

	private static UsuarioAcessoDto ParaDto(UsuarioAcesso a)
	{
		return new UsuarioAcessoDto
		{
			Id = a.Id,
			UsuarioId = a.UsuarioId,
			Sistema = a.Sistema,
			NomeSistema = Enum.IsDefined(typeof(SigeraSystem), a.Sistema)
				? ((SigeraSystem)a.Sistema).ToString()
				: string.Empty,
			CdEmp = a.CdEmp,
			CdFil = a.CdFil,
			SqPesCorretor = a.SqPesCorretor,
			CriadoEmUtc = a.CriadoEmUtc
		};
	}
}
