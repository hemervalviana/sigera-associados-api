using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Referencias;

namespace SigeraAssociados.Application.Interfaces.Referencias;

/// <summary>
/// Referências pessoais do associado (T000REFE), no sistema da requisição,
/// dentro do escopo de acesso do usuário: associado fora dos acessos é
/// tratado como não encontrado.
/// </summary>
public interface IReferenciasAssociadoService
{
	/// <summary>
	/// Null quando o associado não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<ReferenciaAssociadoDto>?> ListarAsync(decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui uma referência; SQ_REF = MAX + 1 da pessoa. Recusa o mesmo
	/// nome e telefone 1 repetidos no associado.
	/// </summary>
	Task<ResultadoTabelaApoio<ReferenciaAssociadoDto>> IncluirAsync(decimal sqPesCli, ReferenciaAssociadoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ReferenciaAssociadoDto>> AlterarAsync(decimal sqPesCli, decimal sqReferencia, ReferenciaAssociadoDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão física.</summary>
	Task<ResultadoTabelaApoio<ReferenciaAssociadoDto>> ExcluirAsync(decimal sqPesCli, decimal sqReferencia, CancellationToken cancellationToken = default);
}
