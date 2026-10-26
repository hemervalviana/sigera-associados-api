namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Confere no banco de negócio de um sistema se a empresa, a filial
/// e o corretor informados numa concessão de acesso existem.
/// </summary>
public interface IVerificadorCadastroSistema
{
	/// <summary>
	/// Empresa (CD_EMP) da base do sistema, lida em T009EMPS.
	/// Retorna null quando a tabela não tem exatamente uma empresa.
	/// </summary>
	Task<decimal?> ObterEmpresaAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// A filial está cadastrada em T000PARA na empresa.
	/// </summary>
	Task<bool> FilialExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal cdFil,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// O corretor está cadastrado em T009CRRT na empresa e, quando a filial
	/// é informada, pertence a essa filial.
	/// </summary>
	Task<bool> CorretorExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal sqPesCorretor,
		decimal? cdFil,
		CancellationToken cancellationToken = default);
}
