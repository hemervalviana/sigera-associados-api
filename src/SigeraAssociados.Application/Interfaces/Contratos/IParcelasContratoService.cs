using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;

namespace SigeraAssociados.Application.Interfaces.Contratos;

/// <summary>
/// Parcelas do contrato (T009PAGA). Cada gravação acerta QT_PCL (e
/// DT_ULT_DCT no produto 2) do contrato. Parcela baixada não muda.
/// </summary>
public interface IParcelasContratoService
{
	/// <summary>Null quando o contrato não existe, foi excluído ou está fora dos acessos.</summary>
	Task<ResumoParcelas?> ListarAsync(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ParcelaDto>> IncluirAsync(decimal cdFil, decimal sqCtr, ParcelaDados dados, string login, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ParcelaDto>> AlterarAsync(decimal cdFil, decimal sqCtr, decimal numeroParcela, ParcelaDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão física; recusada se baixada ou se for a única parcela.</summary>
	Task<ResultadoTabelaApoio<ParcelaDto>> ExcluirAsync(decimal cdFil, decimal sqCtr, decimal numeroParcela, CancellationToken cancellationToken = default);
}
