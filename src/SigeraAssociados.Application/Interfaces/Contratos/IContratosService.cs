using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;

namespace SigeraAssociados.Application.Interfaces.Contratos;

/// <summary>
/// Contratos (T009CTRT) no sistema da requisição, dentro do escopo do
/// usuário: filial e, para quem tem acesso pela carteira, só os contratos
/// do corretor. Contrato fora dos acessos é tratado como não encontrado.
/// </summary>
public interface IContratosService
{
	Task<PesquisaContratosResultado> PesquisarAsync(PesquisaContratosRequest request, CancellationToken cancellationToken = default);

	/// <summary>Detalhe com descrições e parcelas. Null quando não encontrado.</summary>
	Task<ContratoDto?> ObterAsync(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken = default);

	/// <summary>Inclui o contrato (SQ_CTR = MAX + 1 da filial) e gera as parcelas.</summary>
	Task<ResultadoTabelaApoio<ContratoDto>> IncluirAsync(ContratoDados dados, string login, CancellationToken cancellationToken = default);

	/// <summary>Altera o contrato (não mexe nas parcelas).</summary>
	Task<ResultadoTabelaApoio<ContratoDto>> AlterarAsync(decimal cdFil, decimal sqCtr, ContratoDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão lógica (DT_EXC_API e NM_USU_EXC_API); as parcelas ficam.</summary>
	Task<ResultadoTabelaApoio<ContratoResumoDto>> ExcluirAsync(decimal cdFil, decimal sqCtr, string login, CancellationToken cancellationToken = default);

	// ----- Tabelas de apoio (consulta) -----

	Task<IReadOnlyList<ApoioContratoDto>> SituacoesAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ApoioContratoDto>> CarenciasAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ApoioContratoDto>> OrigensRecursoAsync(CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ApoioContratoDto>> ProdutoresAsync(decimal? sqPesCorretor, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<IndiceContratoDto>> IndicesAsync(decimal? cdCarencia, decimal? quantidadeParcelas, CancellationToken cancellationToken = default);
}
