using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Application.Interfaces.Cadastros;

/// <summary>
/// Consulta as tabelas de apoio de um sistema: filiais e corretores
/// (concessão de acesso) e situações, tratamentos e municípios
/// (cadastro de associados).
/// Sistema inválido ou filtros fora da faixa lançam DadosInvalidosException.
/// </summary>
public interface ICadastrosSistemaService
{
	/// <summary>
	/// Filiais do sistema (T000PARA), ordenadas pelo código.
	/// </summary>
	Task<IReadOnlyList<FilialDto>> ListarFiliaisAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Corretores do sistema (T009CRRT), ordenados pelo nome,
	/// com filtro opcional por filial e por nome ou código.
	/// </summary>
	Task<ConsultaCorretoresResultado> ListarCorretoresAsync(
		int sistema,
		ConsultaCorretoresRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Situações do associado (T000SCLI), ordenadas pelo código.
	/// </summary>
	Task<IReadOnlyList<ItemTabelaDto>> ListarSituacoesAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Tratamentos (T000TRTA), ordenados pelo código.
	/// </summary>
	Task<IReadOnlyList<ItemTabelaDto>> ListarTratamentosAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Bancos (T000BANC), ordenados pelo código, com busca opcional
	/// pelo começo do nome ou pelo código.
	/// </summary>
	Task<IReadOnlyList<BancoDados>> ListarBancosAsync(
		int sistema,
		string? busca,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Tipos de endereço (T000TEND), ordenados pelo código.
	/// </summary>
	Task<IReadOnlyList<ItemTabelaDto>> ListarTiposEnderecoAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Tipos de telefone (T000TTEL), ordenados pelo código.
	/// </summary>
	Task<IReadOnlyList<ItemTabelaDto>> ListarTiposTelefoneAsync(
		int sistema,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Municípios (T000MUNI), ordenados pelo nome,
	/// com filtro opcional por UF e por nome ou código.
	/// </summary>
	Task<ConsultaMunicipiosResultado> ListarMunicipiosAsync(
		int sistema,
		ConsultaMunicipiosRequest request,
		CancellationToken cancellationToken = default);
}
