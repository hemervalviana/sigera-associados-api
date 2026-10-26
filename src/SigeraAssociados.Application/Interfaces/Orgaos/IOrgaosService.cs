using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Orgaos;

namespace SigeraAssociados.Application.Interfaces.Orgaos;

/// <summary>
/// Órgãos (T000ORGA) e as tabelas de apoio dos vínculos:
/// lotações (T000LOTA), cargos (T000CARG) e situações do vínculo (T000SPOG);
/// e as do próprio órgão: grupos (T009GRUP) e subórgãos (T009SBOR).
/// Código omitido na inclusão: MAX + 1. Exclusão física, recusada
/// (Conflito) quando o registro está em uso.
/// </summary>
public interface IOrgaosService
{
	Task<ConsultaOrgaosResultado> ListarAsync(int sistema, ConsultaOrgaosRequest request, CancellationToken cancellationToken = default);

	Task<OrgaoDados?> ObterAsync(int sistema, decimal cdOrg, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<OrgaoDados>> IncluirAsync(int sistema, OrgaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<OrgaoDados>> AlterarAsync(int sistema, decimal cdOrg, OrgaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<OrgaoDados>> ExcluirAsync(int sistema, decimal cdOrg, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<LotacaoDados>> ListarLotacoesAsync(int sistema, string? busca, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<LotacaoDados>> IncluirLotacaoAsync(int sistema, LotacaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<LotacaoDados>> AlterarLotacaoAsync(int sistema, decimal cdLot, LotacaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<LotacaoDados>> ExcluirLotacaoAsync(int sistema, decimal cdLot, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<GrupoDados>> ListarGruposAsync(int sistema, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<GrupoDados>> IncluirGrupoAsync(int sistema, GrupoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<GrupoDados>> AlterarGrupoAsync(int sistema, decimal cdGrupo, GrupoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<GrupoDados>> ExcluirGrupoAsync(int sistema, decimal cdGrupo, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<SubOrgaoDados>> ListarSubOrgaosAsync(int sistema, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<SubOrgaoDados>> IncluirSubOrgaoAsync(int sistema, SubOrgaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<SubOrgaoDados>> AlterarSubOrgaoAsync(int sistema, decimal sqSubOrgao, SubOrgaoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<SubOrgaoDados>> ExcluirSubOrgaoAsync(int sistema, decimal sqSubOrgao, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<CargoDados>> ListarCargosAsync(int sistema, string? busca, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<CargoDados>> IncluirCargoAsync(int sistema, CargoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<CargoDados>> AlterarCargoAsync(int sistema, decimal cdCargo, CargoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<CargoDados>> ExcluirCargoAsync(int sistema, decimal cdCargo, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ItemTabelaDto>> ListarSituacoesAsync(int sistema, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirSituacaoAsync(int sistema, ItemTabelaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarSituacaoAsync(int sistema, decimal codigo, ItemTabelaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirSituacaoAsync(int sistema, decimal codigo, CancellationToken cancellationToken = default);
}
