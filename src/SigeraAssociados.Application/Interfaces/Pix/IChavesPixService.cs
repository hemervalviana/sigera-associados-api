using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Pix;

namespace SigeraAssociados.Application.Interfaces.Pix;

/// <summary>
/// Chaves Pix do associado (T009PIX), no sistema da requisição, dentro do
/// escopo de acesso do usuário: associado fora dos acessos é tratado como
/// não encontrado.
/// </summary>
public interface IChavesPixService
{
	/// <summary>
	/// Null quando o associado não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<ChavePixDto>?> ListarAsync(decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui uma chave; SQ_REG = MAX + 1 da pessoa. Recusa a mesma chave repetida no associado.
	/// </summary>
	Task<ResultadoTabelaApoio<ChavePixDto>> IncluirAsync(decimal sqPesCli, ChavePixDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ChavePixDto>> AlterarAsync(decimal sqPesCli, decimal sqPix, ChavePixDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão física.</summary>
	Task<ResultadoTabelaApoio<ChavePixDto>> ExcluirAsync(decimal sqPesCli, decimal sqPix, CancellationToken cancellationToken = default);
}
