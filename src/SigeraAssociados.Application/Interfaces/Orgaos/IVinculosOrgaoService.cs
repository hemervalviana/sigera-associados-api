using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Orgaos;

namespace SigeraAssociados.Application.Interfaces.Orgaos;

/// <summary>
/// Vínculos do associado com órgãos (T000RPOR), no sistema da requisição
/// (cabeçalho X-Sigera-Sistema), dentro do escopo de acesso do usuário:
/// associado fora dos acessos é tratado como não encontrado.
/// </summary>
public interface IVinculosOrgaoService
{
	/// <summary>
	/// Null quando o associado não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<VinculoOrgaoDto>?> ListarAsync(decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui um vínculo; SQ_ORG = MAX + 1 da pessoa.
	/// </summary>
	Task<ResultadoTabelaApoio<VinculoOrgaoDto>> IncluirAsync(decimal sqPesCli, VinculoOrgaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<VinculoOrgaoDto>> AlterarAsync(decimal sqPesCli, decimal sqOrg, VinculoOrgaoDados dados, CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclusão física do vínculo.
	/// </summary>
	Task<ResultadoTabelaApoio<VinculoOrgaoDto>> ExcluirAsync(decimal sqPesCli, decimal sqOrg, CancellationToken cancellationToken = default);
}
