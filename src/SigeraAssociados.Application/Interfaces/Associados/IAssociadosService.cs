using SigeraAssociados.Application.Associados;

namespace SigeraAssociados.Application.Interfaces.Associados;

/// <summary>
/// Define as operações de consulta de associados
/// disponibilizadas pela aplicação.
/// </summary>
public interface IAssociadosService
{
	/// <summary>
	/// Lista os primeiros 10 associados.
	/// </summary>
	Task<IReadOnlyList<AssociadoDto>> ListarAsync(
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Consulta associados com filtros, paginação e ordenação.
	/// </summary>
	Task<ConsultaAssociadosResultado> ConsultarPaginadoAsync(
		ConsultaAssociadosRequest request,
		CancellationToken cancellationToken = default);
}