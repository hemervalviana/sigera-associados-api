using Microsoft.AspNetCore.Identity;

namespace SigeraAssociados.Infrastructure.Persistence.Identity;

/// <summary>
/// Escopo de acesso de um usuário a um sistema SIGERA.
/// Um usuário pode ter várias linhas (vários sistemas, empresas ou filiais).
///
/// Regras de interpretação:
/// - CdEmp nulo: todas as empresas do sistema.
/// - CdFil nulo: todas as filiais da empresa informada.
/// - SqPesCorretor preenchido: somente os associados da carteira
///   desse corretor (contratos em T009CTRT.SQ_PES_CRO) na empresa informada.
/// </summary>
public sealed class UsuarioAcesso
{
	public int Id { get; set; }

	public string UsuarioId { get; set; } = string.Empty;

	/// <summary>
	/// Código do sistema: 1 PROASP, 2 SONASP, 3 ABSP, 4 GSP.
	/// </summary>
	public int Sistema { get; set; }

	public decimal? CdEmp { get; set; }

	public decimal? CdFil { get; set; }

	/// <summary>
	/// Sequência da pessoa corretor (T000PESS.SQ_PES) no banco do sistema.
	/// </summary>
	public decimal? SqPesCorretor { get; set; }

	public DateTime CriadoEmUtc { get; set; }

	public IdentityUser? Usuario { get; set; }
}
