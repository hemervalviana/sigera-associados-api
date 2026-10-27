namespace SigeraAssociados.Application.Orgaos;

/// <summary>
/// Situação ATIVO/INATIVO de uma tabela do legado, com os códigos
/// fixos de cada tabela (sem tabela de situações no banco).
/// </summary>
public sealed class SituacaoAtivoInativo
{
	private SituacaoAtivoInativo(decimal ativo, decimal inativo)
	{
		Ativo = ativo;
		Inativo = inativo;
	}

	/// <summary>
	/// Órgão (T000ORGA.CD_SIT_ORG): 1 = ATIVO, 0 = INATIVO.
	/// </summary>
	public static SituacaoAtivoInativo Orgao { get; } = new(1, 0);

	/// <summary>
	/// Lotação (T000LOTA.CD_SIT_LOT): 1 = ATIVO, 2 = INATIVO.
	/// </summary>
	public static SituacaoAtivoInativo Lotacao { get; } = new(1, 2);

	/// <summary>
	/// Cargo (T000CARG.CD_SIT_CAG): 1 = ATIVO, 0 = INATIVO.
	/// </summary>
	public static SituacaoAtivoInativo Cargo { get; } = new(1, 0);

	/// <summary>
	/// Banco (T000BANC.CD_SIT_BCO): 1 = ATIVO, 0 = INATIVO.
	/// </summary>
	public static SituacaoAtivoInativo Banco { get; } = new(1, 0);

	/// <summary>
	/// Corretor (T009CRRT.CD_SIT_CRRT): 1 = ATIVO, 2 = INATIVO.
	/// </summary>
	public static SituacaoAtivoInativo Corretor { get; } = new(1, 2);

	public decimal Ativo { get; }

	public decimal Inativo { get; }

	/// <summary>
	/// ATIVO, INATIVO ou null (sem situação ou código desconhecido).
	/// </summary>
	public string? Descricao(decimal? codigo) =>
		codigo == Ativo ? "ATIVO"
		: codigo == Inativo ? "INATIVO"
		: null;

	/// <summary>
	/// Vazio (a API grava ATIVO na inclusão) ou um dos dois códigos.
	/// </summary>
	public bool Valida(decimal? codigo) => codigo is null || codigo == Ativo || codigo == Inativo;

	/// <summary>
	/// Mensagem de erro para o campo, ou null quando o código é válido.
	/// </summary>
	public string? Validar(decimal? codigo, string campo) =>
		Valida(codigo) ? null : $"O campo {campo} deve ser {Ativo:0} (ATIVO) ou {Inativo:0} (INATIVO).";
}
