namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Dados para conceder um novo escopo de acesso a um usuário.
/// A empresa não é informada: cada sistema tem uma única empresa.
/// </summary>
public sealed class NovoAcessoDto
{
	/// <summary>
	/// 0 = todos os sistemas; 1 a 4 = um sistema.
	/// </summary>
	public int Sistema { get; init; }

	public decimal? CdFil { get; init; }

	public decimal? SqPesCorretor { get; init; }
}
