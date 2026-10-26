using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Usuarios;
using SigeraAssociados.Application.Usuarios;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;

namespace SigeraAssociados.Infrastructure.Usuarios;

/// <summary>
/// Consulta e mantém as contas do ASP.NET Core Identity.
/// As regras de permissão sobre a conta alvo ficam aqui,
/// para valerem em qualquer endpoint que use o serviço.
/// </summary>
public sealed class GestaoUsuariosService : IGestaoUsuariosService
{
	private readonly SigeraIdentityDbContext _identityDb;
	private readonly UserManager<IdentityUser> _userManager;
	private readonly IUsuarioAtual _usuarioAtual;
	private readonly IMemoryCache _cache;
	private readonly ITokensRenovacaoService _tokensRenovacao;
	private readonly ISessoesService _sessoes;

	public GestaoUsuariosService(
		SigeraIdentityDbContext identityDb,
		UserManager<IdentityUser> userManager,
		IUsuarioAtual usuarioAtual,
		IMemoryCache cache,
		ITokensRenovacaoService tokensRenovacao,
		ISessoesService sessoes)
	{
		_identityDb = identityDb;
		_userManager = userManager;
		_usuarioAtual = usuarioAtual;
		_cache = cache;
		_tokensRenovacao = tokensRenovacao;
		_sessoes = sessoes;
	}

	/// <inheritdoc />
	public async Task<ConsultaUsuariosResultado> ListarAsync(
		ConsultaUsuariosRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.Pagina < 1)
		{
			throw new DadosInvalidosException("A página deve ser maior ou igual a 1.");
		}

		if (request.TamanhoPagina < 1 || request.TamanhoPagina > 100)
		{
			throw new DadosInvalidosException("O tamanho da página deve estar entre 1 e 100.");
		}

		var query = _identityDb.Users.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(request.Busca))
		{
			var busca = request.Busca.Trim().ToUpperInvariant();

			query = query.Where(u =>
				u.NormalizedEmail != null &&
				u.NormalizedEmail.Contains(busca));
		}

		if (!string.IsNullOrWhiteSpace(request.Grupo))
		{
			var grupo = Grupos.Normalizar(request.Grupo)
				?? throw new DadosInvalidosException("Grupo funcional inválido.");

			var grupoId = await _identityDb.Roles
				.Where(r => r.Name == grupo)
				.Select(r => r.Id)
				.FirstOrDefaultAsync(cancellationToken);

			if (grupoId is null)
			{
				return Vazio(request);
			}

			query = query.Where(u => _identityDb.UserRoles.Any(
				ur => ur.UserId == u.Id && ur.RoleId == grupoId));
		}

		var totalRegistros = await query.CountAsync(cancellationToken);

		var pagina = await query
			.OrderBy(u => u.NormalizedEmail)
			.ThenBy(u => u.Id)
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.Select(u => new
			{
				u.Id,
				u.Email,
				u.EmailConfirmed,
				u.LockoutEnd
			})
			.ToListAsync(cancellationToken);

		var gruposPorUsuario = await ObterGruposAsync(
			pagina.Select(u => u.Id).ToArray(),
			cancellationToken);

		var agora = DateTimeOffset.UtcNow;

		return new ConsultaUsuariosResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = totalRegistros,
			TotalPaginas = (int)Math.Ceiling(
				totalRegistros / (double)request.TamanhoPagina),
			Usuarios = pagina
				.Select(u => new UsuarioResumoDto
				{
					Id = u.Id,
					Email = u.Email ?? string.Empty,
					EmailConfirmado = u.EmailConfirmed,
					Bloqueado = u.LockoutEnd is not null && u.LockoutEnd > agora,
					BloqueadoAte = u.LockoutEnd > agora ? u.LockoutEnd : null,
					Grupos = gruposPorUsuario.TryGetValue(u.Id, out var grupos)
						? grupos
						: Array.Empty<string>()
				})
				.ToList()
		};
	}

	/// <inheritdoc />
	public async Task<UsuarioResumoDto?> ObterAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		var usuario = await _userManager.FindByIdAsync(usuarioId);

		return usuario is null
			? null
			: await ParaDtoAsync(usuario);
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoUsuario> AlterarGrupoAsync(
		string usuarioId,
		string grupo,
		CancellationToken cancellationToken = default)
	{
		var novoGrupo = Grupos.Normalizar(grupo);

		if (novoGrupo is null)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Invalido,
				"Informe um grupo funcional válido.");
		}

		if (novoGrupo == Grupos.Administradores)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Invalido,
				"A atribuição do grupo Administradores exige um processo separado.");
		}

		var (usuario, funcoes, erro) = await CarregarAlvoAsync(usuarioId);

		if (erro is not null)
		{
			return erro;
		}

		if (!await _identityDb.Roles.AnyAsync(
				r => r.Name == novoGrupo,
				cancellationToken))
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Falha,
				"O grupo solicitado ainda não foi provisionado no sistema.");
		}

		// Remove os grupos funcionais atuais (inclusive a função legada)
		// e mantém qualquer outra função que não seja de grupo.
		var remover = funcoes
			.Where(f =>
				Grupos.Funcionais.Contains(f, StringComparer.OrdinalIgnoreCase) ||
				string.Equals(f, Grupos.AdministradorLegado, StringComparison.OrdinalIgnoreCase))
			.Where(f => !string.Equals(f, novoGrupo, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		if (remover.Length > 0)
		{
			var resultadoRemocao = await _userManager.RemoveFromRolesAsync(
				usuario!,
				remover);

			if (!resultadoRemocao.Succeeded)
			{
				return Falha("Não foi possível remover os grupos atuais.");
			}
		}

		if (!funcoes.Contains(novoGrupo, StringComparer.OrdinalIgnoreCase))
		{
			var resultadoInclusao = await _userManager.AddToRoleAsync(
				usuario!,
				novoGrupo);

			if (!resultadoInclusao.Succeeded)
			{
				return Falha(
					"Os grupos anteriores foram removidos, mas não foi possível " +
					"atribuir o novo grupo. Tente novamente.");
			}
		}

		// Novo carimbo: o token de acesso atual deixa de valer na hora.
		// O usuário continua logado: a próxima renovação traz o grupo novo.
		await _userManager.UpdateSecurityStampAsync(usuario!);
		_sessoes.InvalidarCache(usuario!.Id);

		return await SucessoAsync(usuario!);
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoUsuario> AdicionarGrupoAsync(
		string usuarioId,
		string grupo,
		CancellationToken cancellationToken = default)
	{
		var novoGrupo = Grupos.Normalizar(grupo);

		if (novoGrupo is null)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Invalido,
				"Informe um grupo funcional válido.");
		}

		if (novoGrupo == Grupos.Administradores)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Invalido,
				"A atribuição do grupo Administradores exige um processo separado.");
		}

		var (usuario, funcoes, erro) = await CarregarAlvoAsync(usuarioId);

		if (erro is not null)
		{
			return erro;
		}

		if (funcoes.Contains(novoGrupo, StringComparer.OrdinalIgnoreCase))
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Conflito,
				$"O usuário já pertence ao grupo {novoGrupo}.");
		}

		if (!await _identityDb.Roles.AnyAsync(
				r => r.Name == novoGrupo,
				cancellationToken))
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Falha,
				"O grupo solicitado ainda não foi provisionado no sistema.");
		}

		var resultado = await _userManager.AddToRoleAsync(usuario!, novoGrupo);

		if (!resultado.Succeeded)
		{
			return Falha("Não foi possível incluir o usuário no grupo.");
		}

		await AplicarMudancaDeGruposAsync(usuario!);

		return await SucessoAsync(usuario!);
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoUsuario> RemoverGrupoAsync(
		string usuarioId,
		string grupo,
		CancellationToken cancellationToken = default)
	{
		// Aceita também a função legada "Administrador", para limpar contas antigas.
		var grupoInformado = Grupos.Normalizar(grupo)
			?? (string.Equals(grupo?.Trim(), Grupos.AdministradorLegado, StringComparison.OrdinalIgnoreCase)
				? Grupos.AdministradorLegado
				: null);

		if (grupoInformado is null)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Invalido,
				"Informe um grupo funcional válido.");
		}

		var (usuario, funcoes, erro) = await CarregarAlvoAsync(usuarioId);

		if (erro is not null)
		{
			return erro;
		}

		var funcaoAtual = funcoes.FirstOrDefault(f =>
			string.Equals(f, grupoInformado, StringComparison.OrdinalIgnoreCase));

		if (funcaoAtual is null)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Conflito,
				$"O usuário não pertence ao grupo {grupoInformado}.");
		}

		var gruposRestantes = funcoes.Count(f =>
			EhGrupoFuncional(f) &&
			!string.Equals(f, funcaoAtual, StringComparison.OrdinalIgnoreCase));

		if (gruposRestantes == 0)
		{
			return new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Invalido,
				"O usuário precisa continuar em pelo menos um grupo. " +
				"Para trocar o grupo, use PUT /api/administracao/usuarios/{id}/grupo.");
		}

		var resultado = await _userManager.RemoveFromRoleAsync(usuario!, funcaoAtual);

		if (!resultado.Succeeded)
		{
			return Falha("Não foi possível remover o usuário do grupo.");
		}

		await AplicarMudancaDeGruposAsync(usuario!);

		return await SucessoAsync(usuario!);
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoUsuario> BloquearAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		var (usuario, _, erro) = await CarregarAlvoAsync(usuarioId);

		if (erro is not null)
		{
			return erro;
		}

		// O bloqueio só vale se o lockout estiver habilitado para a conta.
		if (!usuario!.LockoutEnabled)
		{
			var resultadoHabilitar = await _userManager.SetLockoutEnabledAsync(
				usuario,
				true);

			if (!resultadoHabilitar.Succeeded)
			{
				return Falha("Não foi possível bloquear o usuário.");
			}
		}

		var resultado = await _userManager.SetLockoutEndDateAsync(
			usuario,
			DateTimeOffset.MaxValue);

		if (!resultado.Succeeded)
		{
			return Falha("Não foi possível bloquear o usuário.");
		}

		// Novo carimbo: tokens de acesso, confirmação e redefinição de senha
		// pendentes deixam de valer na hora.
		await _userManager.UpdateSecurityStampAsync(usuario);

		// Encerra as sessões: os tokens de renovação são revogados.
		await _tokensRenovacao.RevogarTodosAsync(
			usuario.Id,
			MotivosRevogacao.Bloqueio,
			cancellationToken);

		_cache.Remove(EscopoAcessoService.ChaveCache(usuario.Id));
		_sessoes.InvalidarCache(usuario.Id);

		return await SucessoAsync(usuario);
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoUsuario> DesbloquearAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		var (usuario, _, erro) = await CarregarAlvoAsync(usuarioId);

		if (erro is not null)
		{
			return erro;
		}

		var resultado = await _userManager.SetLockoutEndDateAsync(
			usuario!,
			null);

		if (!resultado.Succeeded)
		{
			return Falha("Não foi possível desbloquear o usuário.");
		}

		await _userManager.ResetAccessFailedCountAsync(usuario!);

		return await SucessoAsync(usuario!);
	}

	/// <inheritdoc />
	public async Task<ResultadoGestaoUsuario> ExcluirAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		var (usuario, _, erro) = await CarregarAlvoAsync(usuarioId);

		if (erro is not null)
		{
			return erro;
		}

		var dto = await ParaDtoAsync(usuario!);

		// Grupos e acessos (UsuarioAcessos) são removidos em cascata.
		var resultado = await _userManager.DeleteAsync(usuario!);

		if (!resultado.Succeeded)
		{
			return Falha("Não foi possível excluir o usuário.");
		}

		_cache.Remove(EscopoAcessoService.ChaveCache(usuarioId));
		_sessoes.InvalidarCache(usuarioId);

		return new ResultadoGestaoUsuario(
			StatusGestaoUsuario.Sucesso,
			Usuario: dto);
	}

	/// <summary>
	/// Carrega a conta alvo e aplica as regras comuns:
	/// a conta existe, não é a do executor e, se for de administrador,
	/// o executor também é administrador.
	/// </summary>
	private async Task<(IdentityUser? Usuario, IList<string> Funcoes, ResultadoGestaoUsuario? Erro)>
		CarregarAlvoAsync(string usuarioId)
	{
		var usuario = string.IsNullOrWhiteSpace(usuarioId)
			? null
			: await _userManager.FindByIdAsync(usuarioId);

		if (usuario is null)
		{
			return (null, Array.Empty<string>(), new ResultadoGestaoUsuario(
				StatusGestaoUsuario.UsuarioNaoEncontrado,
				"Usuário não encontrado."));
		}

		if (string.Equals(usuario.Id, _usuarioAtual.UsuarioId, StringComparison.Ordinal))
		{
			return (null, Array.Empty<string>(), new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Proibido,
				"Não é permitido alterar, bloquear ou excluir a própria conta."));
		}

		var funcoes = await _userManager.GetRolesAsync(usuario);

		if (Grupos.ContemAdministrador(funcoes) && !_usuarioAtual.PossuiAcessoIrrestrito)
		{
			return (null, Array.Empty<string>(), new ResultadoGestaoUsuario(
				StatusGestaoUsuario.Proibido,
				"Somente administradores podem alterar contas de administradores."));
		}

		return (usuario, funcoes, null);
	}

	/// <summary>
	/// Faz a mudança de grupos valer na hora: o token de acesso atual
	/// deixa de valer e a próxima renovação já traz os grupos novos.
	/// O usuário continua logado.
	/// </summary>
	private async Task AplicarMudancaDeGruposAsync(IdentityUser usuario)
	{
		await _userManager.UpdateSecurityStampAsync(usuario);

		// O escopo depende dos grupos (acesso irrestrito dos administradores).
		_cache.Remove(EscopoAcessoService.ChaveCache(usuario.Id));
		_sessoes.InvalidarCache(usuario.Id);
	}

	private static bool EhGrupoFuncional(string funcao)
	{
		return Grupos.Funcionais.Contains(funcao, StringComparer.OrdinalIgnoreCase) ||
			string.Equals(funcao, Grupos.AdministradorLegado, StringComparison.OrdinalIgnoreCase);
	}

	private async Task<Dictionary<string, IReadOnlyList<string>>> ObterGruposAsync(
		string[] usuarioIds,
		CancellationToken cancellationToken)
	{
		if (usuarioIds.Length == 0)
		{
			return new Dictionary<string, IReadOnlyList<string>>();
		}

		var linhas = await (
				from ur in _identityDb.UserRoles
				join r in _identityDb.Roles on ur.RoleId equals r.Id
				where usuarioIds.Contains(ur.UserId)
				select new { ur.UserId, r.Name })
			.AsNoTracking()
			.ToListAsync(cancellationToken);

		return linhas
			.Where(l => l.Name != null)
			.GroupBy(l => l.UserId)
			.ToDictionary(
				g => g.Key,
				g => (IReadOnlyList<string>)g
					.Select(l => l.Name!)
					.OrderBy(n => n)
					.ToList());
	}

	private async Task<UsuarioResumoDto> ParaDtoAsync(IdentityUser usuario)
	{
		var funcoes = await _userManager.GetRolesAsync(usuario);
		var agora = DateTimeOffset.UtcNow;

		return new UsuarioResumoDto
		{
			Id = usuario.Id,
			Email = usuario.Email ?? string.Empty,
			EmailConfirmado = usuario.EmailConfirmed,
			Bloqueado = usuario.LockoutEnd is not null && usuario.LockoutEnd > agora,
			BloqueadoAte = usuario.LockoutEnd > agora ? usuario.LockoutEnd : null,
			Grupos = funcoes.OrderBy(f => f).ToList()
		};
	}

	private async Task<ResultadoGestaoUsuario> SucessoAsync(IdentityUser usuario)
	{
		return new ResultadoGestaoUsuario(
			StatusGestaoUsuario.Sucesso,
			Usuario: await ParaDtoAsync(usuario));
	}

	private static ResultadoGestaoUsuario Falha(string mensagem)
	{
		return new ResultadoGestaoUsuario(StatusGestaoUsuario.Falha, mensagem);
	}

	private static ConsultaUsuariosResultado Vazio(ConsultaUsuariosRequest request)
	{
		return new ConsultaUsuariosResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = 0,
			TotalPaginas = 0
		};
	}
}
