namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Representa os dados não sensíveis de um associado.
/// </summary>
public sealed class AssociadoDto
{
	public decimal CDEmp { get; init; }

	public decimal SQPesCli { get; init; }

	public string? Nome { get; init; }

	/// <summary>
	/// Tipo de pessoa: F = física, J = jurídica.
	/// </summary>
	public string? TipoPessoa { get; init; }

	/// <summary>
	/// CPF mascarado (***.456.789-**). Nulo para pessoa jurídica ou sem CPF.
	/// </summary>
	public string? Cpf { get; init; }

	public decimal? CDSitCli { get; init; }

	public string? Situacao { get; init; }

	public decimal? CDFil { get; init; }

	public DateTime? DTCarteira { get; init; }
}