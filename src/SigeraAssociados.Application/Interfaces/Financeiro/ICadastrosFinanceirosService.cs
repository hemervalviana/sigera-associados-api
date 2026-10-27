using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Interfaces.Financeiro;

/// <summary>
/// Formas de pagamento (T009FRPG), produtos (T009PRDT) e convênios (T009CONV)
/// no banco do sistema informado. Código omitido na inclusão: MAX + 1.
/// Exclusão física, recusada (Conflito) quando o registro está em uso.
/// </summary>
public interface ICadastrosFinanceirosService
{
	Task<IReadOnlyList<FormaPagamentoDados>> ListarFormasPagamentoAsync(int sistema, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<FormaPagamentoDados>> IncluirFormaPagamentoAsync(int sistema, FormaPagamentoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<FormaPagamentoDados>> AlterarFormaPagamentoAsync(int sistema, decimal codigo, FormaPagamentoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<FormaPagamentoDados>> ExcluirFormaPagamentoAsync(int sistema, decimal codigo, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ProdutoDados>> ListarProdutosAsync(int sistema, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ProdutoDados>> IncluirProdutoAsync(int sistema, ProdutoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ProdutoDados>> AlterarProdutoAsync(int sistema, decimal codigo, ProdutoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ProdutoDados>> ExcluirProdutoAsync(int sistema, decimal codigo, CancellationToken cancellationToken = default);

	/// <summary>
	/// Convênios ordenados pelo nome; `busca` é o começo do nome ou o código.
	/// </summary>
	Task<IReadOnlyList<ConvenioResumoDto>> ListarConveniosAsync(int sistema, string? busca, CancellationToken cancellationToken = default);

	Task<ConvenioDados?> ObterConvenioAsync(int sistema, decimal cdCvn, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ConvenioDados>> IncluirConvenioAsync(int sistema, ConvenioDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ConvenioDados>> AlterarConvenioAsync(int sistema, decimal cdCvn, ConvenioDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ConvenioDados>> ExcluirConvenioAsync(int sistema, decimal cdCvn, CancellationToken cancellationToken = default);
}
