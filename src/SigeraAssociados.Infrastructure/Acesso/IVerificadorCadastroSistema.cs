namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Confere no banco de negócio de um sistema se a empresa, a filial
/// e o corretor informados numa concessão de acesso existem.
/// </summary>
public interface IVerificadorCadastroSistema
{
	/// <summary>
	/// A empresa existe em T009EMPS do sistema.
	/// </summary>
	Task<bool> EmpresaExisteAsync(
		int sistema,
		decimal cdEmp,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// A filial tem pelo menos um associado (T000CLIE) ou contrato (T009CTRT)
	/// na empresa.
	/// </summary>
	Task<bool> FilialExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal cdFil,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// O corretor existe em T000PESS e tem pelo menos um contrato
	/// (T009CTRT.SQ_PES_CRO) na empresa.
	/// </summary>
	Task<bool> CorretorExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal sqPesCorretor,
		CancellationToken cancellationToken = default);
}
