namespace SigeraAssociados.Application.Usuarios;

/// <summary>
/// Página de usuários retornada pela listagem.
/// </summary>
public sealed class ConsultaUsuariosResultado
{
	public int Pagina { get; init; }

	public int TamanhoPagina { get; init; }

	public int TotalRegistros { get; init; }

	public int TotalPaginas { get; init; }

	public IReadOnlyList<UsuarioResumoDto> Usuarios { get; init; } =
		Array.Empty<UsuarioResumoDto>();
}
