using SigeraAssociados.Application.Acesso;

namespace SigeraAssociados.Application.Interfaces.Acesso;

/// <summary>
/// Resolve os escopos de dados do usuário atual.
/// </summary>
public interface IEscopoAcessoService
{
	/// <summary>
	/// Indica se o usuário atual pode acessar o sistema informado.
	/// </summary>
	Task<bool> PossuiAcessoAoSistemaAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Obtém o escopo do usuário atual para o sistema selecionado na requisição.
	/// </summary>
	Task<EscopoConsulta> ObterEscopoAsync(
		CancellationToken cancellationToken = default);
}
