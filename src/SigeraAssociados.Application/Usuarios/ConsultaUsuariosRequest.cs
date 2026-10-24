namespace SigeraAssociados.Application.Usuarios;

/// <summary>
/// Filtros e paginação da listagem de usuários.
/// </summary>
public sealed class ConsultaUsuariosRequest
{
	/// <summary>
	/// Trecho do e-mail.
	/// </summary>
	public string? Busca { get; init; }

	/// <summary>
	/// Nome do grupo funcional.
	/// </summary>
	public string? Grupo { get; init; }

	public int Pagina { get; init; } = 1;

	public int TamanhoPagina { get; init; } = 20;
}
