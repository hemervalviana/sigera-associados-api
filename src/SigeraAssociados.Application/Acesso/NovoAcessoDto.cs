namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Dados para conceder um novo escopo de acesso a um usuário.
/// </summary>
public sealed class NovoAcessoDto
{
	public int Sistema { get; init; }

	public decimal? CdEmp { get; init; }

	public decimal? CdFil { get; init; }

	public decimal? SqPesCorretor { get; init; }
}
