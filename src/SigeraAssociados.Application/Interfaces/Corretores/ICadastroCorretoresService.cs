using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Corretores;

namespace SigeraAssociados.Application.Interfaces.Corretores;

/// <summary>
/// Corretores (T000PESS + T000PFIS ou T000PJUR + T009CRRT) no sistema da
/// requisição. Só aparecem corretores não excluídos e com cadastro coerente
/// com o tipo (física com T000PFIS e sem T000PJUR; jurídica o contrário).
/// Exclusão lógica (T009CRRT.DT_EXC_API), como a do associado.
/// </summary>
public interface ICadastroCorretoresService
{
	Task<PesquisaCorretoresResultado> PesquisarAsync(PesquisaCorretoresRequest request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Null quando não existe, foi excluído ou está fora da regra física/jurídica.
	/// A senha do representante só vem com incluirSenha.
	/// </summary>
	Task<CorretorCadastroDto?> ObterAsync(decimal sqPesCorretor, bool incluirSenha, CancellationToken cancellationToken = default);

	/// <summary>Inclui; SQ_PES = MAX + 1. CPF/CNPJ repetido: Conflito.</summary>
	Task<ResultadoTabelaApoio<CorretorCadastroDto>> IncluirAsync(CadastroCorretorDados dados, string login, CancellationToken cancellationToken = default);

	/// <summary>Altera; o tipo de pessoa não muda.</summary>
	Task<ResultadoTabelaApoio<CorretorCadastroDto>> AlterarAsync(decimal sqPesCorretor, CadastroCorretorDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão lógica: grava data e usuário em T009CRRT; nada é apagado.</summary>
	Task<ResultadoTabelaApoio<CorretorCadastroDto>> ExcluirAsync(decimal sqPesCorretor, string login, CancellationToken cancellationToken = default);
}
