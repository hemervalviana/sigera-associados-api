using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Application.Telefones;

namespace SigeraAssociados.Application.Interfaces.Telefones;

/// <summary>
/// Telefones do associado (T000TELE), no sistema da requisição, dentro do
/// escopo de acesso do usuário: associado fora dos acessos é tratado como
/// não encontrado.
/// </summary>
public interface ITelefonesAssociadoService
{
	/// <summary>
	/// Null quando o associado não existe, foi excluído ou está fora dos acessos.
	/// </summary>
	Task<IReadOnlyList<TelefoneAssociadoDto>?> ListarAsync(TitularCadastro titular, decimal sqPesCli, CancellationToken cancellationToken = default);

	/// <summary>
	/// Inclui um telefone; SQ_TELE = MAX + 1 da empresa. Recusa o mesmo
	/// DDD e número repetido no associado.
	/// </summary>
	Task<ResultadoTabelaApoio<TelefoneAssociadoDto>> IncluirAsync(TitularCadastro titular, decimal sqPesCli, TelefoneAssociadoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<TelefoneAssociadoDto>> AlterarAsync(TitularCadastro titular, decimal sqPesCli, decimal sqTelefone, TelefoneAssociadoDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão física.</summary>
	Task<ResultadoTabelaApoio<TelefoneAssociadoDto>> ExcluirAsync(TitularCadastro titular, decimal sqPesCli, decimal sqTelefone, CancellationToken cancellationToken = default);
}
