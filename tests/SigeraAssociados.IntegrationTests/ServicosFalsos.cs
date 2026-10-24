using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Usuarios;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Gestão de usuários falsa: evita o banco de identidade e permite
/// escolher o resultado que o controller vai receber.
/// </summary>
public sealed class GestaoUsuariosFalso : IGestaoUsuariosService
{
	public const string IdInexistente = "inexistente";

	/// <summary>
	/// Resultado devolvido pelas operações de alteração.
	/// </summary>
	public ResultadoGestaoUsuario ProximoResultado { get; set; } = Sucesso();

	/// <summary>
	/// Quando preenchida, a listagem lança esta exceção
	/// (simula uma falha inesperada).
	/// </summary>
	public Exception? ExcecaoAoListar { get; set; }

	public static UsuarioResumoDto Usuario(string id = "usuario-alvo") => new()
	{
		Id = id,
		Email = $"{id}@teste.local",
		EmailConfirmado = true,
		Grupos = new[] { Grupos.Corretores }
	};

	public static ResultadoGestaoUsuario Sucesso() =>
		new(StatusGestaoUsuario.Sucesso, Usuario: Usuario());

	public Task<ConsultaUsuariosResultado> ListarAsync(
		ConsultaUsuariosRequest request,
		CancellationToken cancellationToken = default)
	{
		if (ExcecaoAoListar is not null)
		{
			throw ExcecaoAoListar;
		}

		return Task.FromResult(new ConsultaUsuariosResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = 1,
			TotalPaginas = 1,
			Usuarios = new[] { Usuario() }
		});
	}

	public Task<UsuarioResumoDto?> ObterAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(usuarioId == IdInexistente ? null : Usuario(usuarioId));
	}

	public Task<ResultadoGestaoUsuario> AlterarGrupoAsync(
		string usuarioId, string grupo, CancellationToken cancellationToken = default)
		=> Task.FromResult(ProximoResultado);

	public Task<ResultadoGestaoUsuario> BloquearAsync(
		string usuarioId, CancellationToken cancellationToken = default)
		=> Task.FromResult(ProximoResultado);

	public Task<ResultadoGestaoUsuario> DesbloquearAsync(
		string usuarioId, CancellationToken cancellationToken = default)
		=> Task.FromResult(ProximoResultado);

	public Task<ResultadoGestaoUsuario> ExcluirAsync(
		string usuarioId, CancellationToken cancellationToken = default)
		=> Task.FromResult(ProximoResultado);
}

/// <summary>
/// Gestão de acessos falsa: lista vazia e operações sempre bem-sucedidas.
/// </summary>
public sealed class GestaoAcessosFalso : IGestaoAcessosService
{
	public Task<IReadOnlyList<UsuarioAcessoDto>?> ListarAsync(
		string usuarioId, CancellationToken cancellationToken = default)
		=> Task.FromResult<IReadOnlyList<UsuarioAcessoDto>?>(Array.Empty<UsuarioAcessoDto>());

	public Task<ResultadoGestaoAcesso> ConcederAsync(
		string usuarioId, NovoAcessoDto acesso, CancellationToken cancellationToken = default)
		=> Task.FromResult(new ResultadoGestaoAcesso(StatusGestaoAcesso.Sucesso, Acesso: new UsuarioAcessoDto { Id = 1, UsuarioId = usuarioId, Sistema = acesso.Sistema }));

	public Task<ResultadoGestaoAcesso> RevogarAsync(
		string usuarioId, int acessoId, CancellationToken cancellationToken = default)
		=> Task.FromResult(new ResultadoGestaoAcesso(StatusGestaoAcesso.Sucesso, Acesso: new UsuarioAcessoDto { Id = acessoId, UsuarioId = usuarioId }));
}
