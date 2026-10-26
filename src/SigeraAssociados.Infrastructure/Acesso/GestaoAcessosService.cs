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
	private readonly IVerificadorCadastroSistema _verificador;

	public GestaoAcessosService(
		SigeraIdentityDbContext identityDb,
		IMemoryCache cache,
		IVerificadorCadastroSistema verificador)
	{
		_identityDb = identityDb;
		_verificador = verificador;
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

		var todos = acesso.Sistema == SigeraSystemCatalog.Todos;

		var sistemas = todos
			? SigeraSystemCatalog.Codigos.ToArray()
			: new[] { acesso.Sistema };

		// O índice único do SQL Server ignora linhas com colunas nulas,
		// por isso a duplicidade também é verificada aqui.
		// A empresa não entra na comparação: cada sistema tem uma só.
		var sistemasJaCadastrados = await _identityDb.UsuarioAcessos
			.Where(a => a.UsuarioId == usuarioId
				&& sistemas.Contains(a.Sistema)
				&& a.CdFil == acesso.CdFil
				&& a.SqPesCorretor == acesso.SqPesCorretor)
			.Select(a => a.Sistema)
			.Distinct()
			.ToListAsync(cancellationToken);

		var sistemasNovos = sistemas.Except(sistemasJaCadastrados).ToArray();

		if (sistemasNovos.Length == 0)
		{
			return new ResultadoGestaoAcesso(
				StatusGestaoAcesso.Duplicado,
				todos
					? "O usuário já tem acesso a todos os sistemas."
					: "Este acesso já está cadastrado para o usuário.");
		}

		// Filial e corretor: confere no banco do sistema e usa a empresa da base.
		decimal? empresa = null;

		if (acesso.CdFil is not null || acesso.SqPesCorretor is not null)
		{
			var (empresaDaBase, erroCadastro) = await VerificarCadastroAsync(
				acesso,
				cancellationToken);

			if (erroCadastro is not null)
			{
				return new ResultadoGestaoAcesso(StatusGestaoAcesso.Invalido, erroCadastro);
			}

			empresa = empresaDaBase;
		}

		var agora = DateTime.UtcNow;

		var entidades = sistemasNovos
			.Select(sistema => new UsuarioAcesso
			{
				UsuarioId = usuarioId,
				Sistema = sistema,
				CdEmp = empresa,
				CdFil = acesso.CdFil,
				SqPesCorretor = acesso.SqPesCorretor,
				CriadoEmUtc = agora
			})
			.ToList();

		_identityDb.UsuarioAcessos.AddRange(entidades);
		await _identityDb.SaveChangesAsync(cancellationToken);

		_cache.Remove(EscopoAcessoService.ChaveCache(usuarioId));

		var dtos = entidades.Select(ParaDto).ToList();

		var mensagem = !todos
			? "Acesso concedido."
			: sistemasJaCadastrados.Count == 0
				? "Acesso concedido a todos os sistemas."
				: $"Acesso concedido a {dtos.Count} sistema(s); os demais já estavam liberados.";

		return new ResultadoGestaoAcesso(
			StatusGestaoAcesso.Sucesso,
			mensagem,
			Acesso: dtos[0],
			Acessos: dtos);
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
		if (acesso.Sistema != SigeraSystemCatalog.Todos
			&& !SigeraSystemCatalog.TryGetDatabase(acesso.Sistema, out _))
		{
			return "Sistema inválido. Os valores permitidos são 0 (todos), 1, 2, 3 e 4.";
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

		if (acesso.Sistema == SigeraSystemCatalog.Todos
			&& (acesso.CdFil is not null || acesso.SqPesCorretor is not null))
		{
			return "A opção todos (sistema 0) libera os sistemas inteiros. " +
				"Para restringir por filial ou corretor, escolha um sistema de 1 a 4.";
		}

		return null;
	}

	/// <summary>
	/// Para acesso por filial ou corretor: obtém a empresa da base do sistema
	/// (gravada na linha de acesso e usada no filtro) e confere se a filial e
	/// o corretor existem. Devolve a empresa ou a mensagem de erro.
	/// </summary>
	private async Task<(decimal? Empresa, string? Erro)> VerificarCadastroAsync(
		NovoAcessoDto acesso,
		CancellationToken cancellationToken)
	{
		var nomeSistema = ((SigeraSystem)acesso.Sistema).ToString();

		if (await _verificador.ObterEmpresaAsync(acesso.Sistema, cancellationToken)
			is not decimal empresa)
		{
			return (null,
				$"Não foi possível identificar a empresa do sistema {nomeSistema}: " +
				"a tabela T009EMPS deve ter exatamente uma empresa.");
		}

		if (acesso.CdFil is decimal filial
			&& !await _verificador.FilialExisteAsync(acesso.Sistema, empresa, filial, cancellationToken))
		{
			return (null, $"A filial {filial:0} não está cadastrada (T000PARA) no sistema {nomeSistema}.");
		}

		if (acesso.SqPesCorretor is decimal corretor
			&& !await _verificador.CorretorExisteAsync(acesso.Sistema, empresa, corretor, acesso.CdFil, cancellationToken))
		{
			return (null, acesso.CdFil is decimal filialDoCorretor
				? $"O corretor {corretor:0} não está cadastrado (T009CRRT) na filial {filialDoCorretor:0} do sistema {nomeSistema}."
				: $"O corretor {corretor:0} não está cadastrado (T009CRRT) no sistema {nomeSistema}.");
		}

		return (empresa, null);
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
			CdFil = a.CdFil,
			SqPesCorretor = a.SqPesCorretor,
			CriadoEmUtc = a.CriadoEmUtc
		};
	}
}
