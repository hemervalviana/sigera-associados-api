using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Application.Interfaces.Cadastros;

/// <summary>
/// Inclusão, alteração e exclusão nas tabelas de apoio do sistema:
/// filiais (T000PARA), situações do associado (T000SCLI),
/// tratamentos (T000TRTA) e municípios (T000MUNI).
///
/// - Código omitido na inclusão (filial, situação e tratamento): MAX + 1.
/// - Exclusão física, recusada (Conflito) quando o registro está em uso.
/// - A situação 10 (falecido) não pode ser excluída: a trigger
///   TR_CLIE_UPD depende dela.
/// </summary>
public interface IManutencaoTabelasApoioService
{
	Task<FilialDados?> ObterFilialAsync(int sistema, decimal cdFil, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<FilialDados>> IncluirFilialAsync(int sistema, FilialDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<FilialDados>> AlterarFilialAsync(int sistema, decimal cdFil, FilialDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<FilialDados>> ExcluirFilialAsync(int sistema, decimal cdFil, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirSituacaoAsync(int sistema, ItemTabelaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarSituacaoAsync(int sistema, decimal codigo, ItemTabelaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirSituacaoAsync(int sistema, decimal codigo, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirTratamentoAsync(int sistema, ItemTabelaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarTratamentoAsync(int sistema, decimal codigo, ItemTabelaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirTratamentoAsync(int sistema, decimal codigo, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<MunicipioDto>> IncluirMunicipioAsync(int sistema, MunicipioDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<MunicipioDto>> AlterarMunicipioAsync(int sistema, decimal cdMun, MunicipioDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<MunicipioDto>> ExcluirMunicipioAsync(int sistema, decimal cdMun, CancellationToken cancellationToken = default);
}
