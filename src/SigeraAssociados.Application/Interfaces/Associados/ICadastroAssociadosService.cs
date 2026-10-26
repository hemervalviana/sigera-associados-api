using SigeraAssociados.Application.Associados;

namespace SigeraAssociados.Application.Interfaces.Associados;

/// <summary>
/// Inclusão, alteração, exclusão lógica e consulta completa de associados
/// pessoa física no sistema escolhido (cabeçalho X-Sigera-Sistema),
/// sempre dentro do escopo de acesso do usuário.
/// </summary>
public interface ICadastroAssociadosService
{
	/// <summary>
	/// Cadastro completo. Null quando não existe, foi excluído, é pessoa
	/// jurídica ou está fora dos acessos do usuário.
	/// </summary>
	Task<AssociadoCadastroDto?> ObterAsync(
		decimal sqPesCli,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Grava T000PESS, T000PFIS e T000CLIE numa transação, com SQ_PES = MAX + 1.
	/// </summary>
	Task<ResultadoCadastroAssociado> IncluirAsync(
		CadastroAssociadoDados dados,
		ExecutorCadastro executor,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Substitui os dados do associado. Só as colunas que mudaram são gravadas.
	/// </summary>
	Task<ResultadoCadastroAssociado> AlterarAsync(
		decimal sqPesCli,
		CadastroAssociadoDados dados,
		ExecutorCadastro executor,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Exclusão lógica: grava data e usuário em T000CLIE (DT_EXC_API e
	/// NM_USU_EXC_API). O associado some da API; nada é apagado.
	/// </summary>
	Task<ResultadoCadastroAssociado> ExcluirAsync(
		decimal sqPesCli,
		ExecutorCadastro executor,
		CancellationToken cancellationToken = default);
}
