using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Application.ContasBancarias;

namespace SigeraAssociados.Application.Interfaces.ContasBancarias;

/// <summary>
/// Contas bancárias do associado (T000CCOR), no sistema da requisição,
/// dentro do escopo de acesso do usuário: associado fora dos acessos é
/// tratado como não encontrado.
/// </summary>
public interface IContasBancariasService
{
	/// <summary>
	/// Null quando o associado não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<ContaBancariaDto>?> ListarAsync(TitularCadastro titular, decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui uma conta; SQ_CTA_COR = MAX + 1 da pessoa. A mesma conta
	/// (banco, agência e número) não se repete para a pessoa.
	/// </summary>
	Task<ResultadoTabelaApoio<ContaBancariaDto>> IncluirAsync(TitularCadastro titular, decimal sqPesCli, ContaBancariaDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ContaBancariaDto>> AlterarAsync(TitularCadastro titular, decimal sqPesCli, decimal sqConta, ContaBancariaDados dados, CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclusão física.
	/// </summary>
	Task<ResultadoTabelaApoio<ContaBancariaDto>> ExcluirAsync(TitularCadastro titular, decimal sqPesCli, decimal sqConta, CancellationToken cancellationToken = default);
}
