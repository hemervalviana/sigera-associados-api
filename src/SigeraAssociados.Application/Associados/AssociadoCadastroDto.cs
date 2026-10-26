namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Cadastro completo de um associado pessoa física, para a tela de edição.
/// O CPF vem completo (diferente da pesquisa, que mascara).
/// </summary>
public sealed class AssociadoCadastroDto
{
	public decimal SqPesCli { get; init; }

	public string Nome { get; init; } = string.Empty;

	public string? NomeCurto { get; init; }

	public string? Email { get; init; }

	public string? Observacao { get; init; }

	/// <summary>
	/// T000PESS.DT_CAD.
	/// </summary>
	public DateTime DataCadastro { get; init; }

	/// <summary>
	/// T000PESS.NM_USU.
	/// </summary>
	public string? UsuarioCadastro { get; init; }

	/// <summary>
	/// Descrição da situação (T000SCLI.DE_SIT_CLI).
	/// </summary>
	public string? Situacao { get; init; }

	/// <summary>
	/// Corretor vinculado no cadastro (T000CLIE.SQ_PES_CRO_CAD).
	/// </summary>
	public decimal? SqPesCorretorCadastro { get; init; }

	public PessoaFisicaDados PessoaFisica { get; init; } = new();

	public AssociadoDados Associado { get; init; } = new();
}
