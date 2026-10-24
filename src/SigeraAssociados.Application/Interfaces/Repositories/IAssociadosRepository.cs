using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Associados;

namespace SigeraAssociados.Application.Interfaces.Repositories;

/// <summary>
/// Define as consultas de persistência disponíveis
/// para o cadastro de associados.
/// Todas as consultas são limitadas pelo escopo de acesso informado.
/// </summary>
public interface IAssociadosRepository
{
	/// <summary>
	/// Lista os primeiros 10 associados dentro do escopo.
	/// </summary>
	Task<IReadOnlyList<AssociadoDto>> ListarAsync(
			EscopoConsulta escopo,
			CancellationToken cancellationToken = default);

	/// <summary>
	/// Consulta um associado pela empresa e sequência da pessoa.
	/// Retorna null quando não encontrado ou fora do escopo.
	/// </summary>
	Task<AssociadoDto?> ConsultarPorCodigoAsync(
			decimal cdEmp,
			decimal sqPesCli,
			EscopoConsulta escopo,
			CancellationToken cancellationToken = default);

	/// <summary>
	/// Consulta associados com filtros, paginação e ordenação,
	/// dentro do escopo.
	/// </summary>
	Task<ConsultaAssociadosResultado> ConsultarPaginadoAsync(
			ConsultaAssociadosRequest request,
			EscopoConsulta escopo,
			CancellationToken cancellationToken = default);
}
