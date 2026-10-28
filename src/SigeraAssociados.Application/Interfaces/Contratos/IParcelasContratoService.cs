using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;

namespace SigeraAssociados.Application.Interfaces.Contratos;

/// <summary>
/// Parcelas do contrato (T009PAGA), com a baixa manual do legado. Cada
/// gravação acerta QT_PCL (e DT_ULT_DCT no produto 2) do contrato. Contrato
/// quitado (9) trava as parcelas.
/// </summary>
public interface IParcelasContratoService
{
	/// <summary>Null quando o contrato não existe, foi excluído ou está fora dos acessos.</summary>
	Task<ResumoParcelas?> ListarAsync(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ParcelaDto>> IncluirAsync(decimal cdFil, decimal sqCtr, ParcelaDados dados, string login, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ParcelaDto>> AlterarAsync(decimal cdFil, decimal sqCtr, decimal numeroParcela, ParcelaDados dados, CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclusão física. Parcela paga exige podeExcluirPaga; não paga,
	/// podeExcluirNaoPaga (senão Proibido). A única parcela não é excluída.
	/// </summary>
	Task<ResultadoTabelaApoio<ParcelaDto>> ExcluirAsync(decimal cdFil, decimal sqCtr, decimal numeroParcela, bool podeExcluirPaga, bool podeExcluirNaoPaga, CancellationToken cancellationToken = default);
}
