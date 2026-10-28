using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;

namespace SigeraAssociados.Application.Interfaces.Contratos;

/// <summary>
/// Categorias do contrato (T009CAT): consulta para todos; gravação só
/// administradores (o controle de perfil fica no controller).
/// </summary>
public interface ICategoriasContratoService
{
	/// <summary>Começo do nome ou código (opcional); ordenadas pelo nome.</summary>
	Task<IReadOnlyList<CategoriaContratoDados>> ListarAsync(string? busca, CancellationToken cancellationToken = default);

	/// <summary>Código omitido: MAX + 1 (UPDLOCK, HOLDLOCK). Código existente: Conflito.</summary>
	Task<ResultadoTabelaApoio<CategoriaContratoDados>> IncluirAsync(CategoriaContratoDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<CategoriaContratoDados>> AlterarAsync(decimal cdCategoria, CategoriaContratoDados dados, CancellationToken cancellationToken = default);

	/// <summary>Exclusão física, recusada (Conflito) se houver contrato com a categoria.</summary>
	Task<ResultadoTabelaApoio<CategoriaContratoDados>> ExcluirAsync(decimal cdCategoria, CancellationToken cancellationToken = default);
}
