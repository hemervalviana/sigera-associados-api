using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Application.Enderecos;

namespace SigeraAssociados.Application.Interfaces.Enderecos;

/// <summary>
/// Endereços do associado (T000ENDE), no sistema da requisição, dentro do
/// escopo de acesso do usuário: associado fora dos acessos é tratado como
/// não encontrado. Sempre há um único endereço principal.
/// </summary>
public interface IEnderecosAssociadoService
{
	/// <summary>
	/// Null quando o associado não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<EnderecoAssociadoDto>?> ListarAsync(TitularCadastro titular, decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui um endereço; SQ_EN_PES = MAX + 1 da pessoa.
	/// </summary>
	Task<ResultadoTabelaApoio<EnderecoAssociadoDto>> IncluirAsync(TitularCadastro titular, decimal sqPesCli, EnderecoAssociadoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<EnderecoAssociadoDto>> AlterarAsync(TitularCadastro titular, decimal sqPesCli, decimal sqEndereco, EnderecoAssociadoDados dados, CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclusão física. Se era o principal, o endereço de menor sequência
	/// que sobrar passa a ser o principal.
	/// </summary>
	Task<ResultadoTabelaApoio<EnderecoAssociadoDto>> ExcluirAsync(TitularCadastro titular, decimal sqPesCli, decimal sqEndereco, CancellationToken cancellationToken = default);
}
