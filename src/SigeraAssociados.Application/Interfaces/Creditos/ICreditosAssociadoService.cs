using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Creditos;

namespace SigeraAssociados.Application.Interfaces.Creditos;

/// <summary>
/// Créditos do associado (T009EXTRA), no sistema da requisição, dentro do
/// escopo de acesso do usuário: associado fora dos acessos é tratado como
/// não encontrado.
/// </summary>
public interface ICreditosAssociadoService
{
	/// <summary>
	/// Créditos do associado, o mais recente primeiro. Null quando o associado
	/// não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<CreditoDto>?> ListarAsync(decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui um crédito; SQ_PGT = MAX + 1 do associado e saldo pela forma de pagamento.
	/// </summary>
	Task<ResultadoTabelaApoio<CreditoDto>> IncluirAsync(decimal sqPesCli, CreditoDados dados, CancellationToken cancellationToken = default);

	/// <summary>
	/// Altera um crédito. Valor e forma de pagamento só mudam com o saldo
	/// intacto (saldo = valor; senão Conflito), e então o saldo é recalculado.
	/// Os demais campos podem ser alterados sempre.
	/// </summary>
	Task<ResultadoTabelaApoio<CreditoDto>> AlterarAsync(decimal sqPesCli, decimal sqCredito, CreditoDados dados, CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclusão física, recusada (Conflito) quando o valor é diferente do saldo.
	/// </summary>
	Task<ResultadoTabelaApoio<CreditoDto>> ExcluirAsync(decimal sqPesCli, decimal sqCredito, CancellationToken cancellationToken = default);
}
