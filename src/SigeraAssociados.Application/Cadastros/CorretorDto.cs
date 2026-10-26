namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Corretor cadastrado em T009CRRT, com o nome de T000PESS.
/// </summary>
public sealed class CorretorDto
{
	/// <summary>
	/// Código da pessoa do corretor: o valor de sqPesCorretor
	/// na concessão de acesso.
	/// </summary>
	public decimal SqPesCorretor { get; init; }

	public string Nome { get; init; } = string.Empty;

	public decimal? CdFil { get; init; }

	/// <summary>
	/// Situação do corretor (T009CRRT.CD_SIT_CRRT).
	/// </summary>
	public decimal? CdSituacao { get; init; }
}
