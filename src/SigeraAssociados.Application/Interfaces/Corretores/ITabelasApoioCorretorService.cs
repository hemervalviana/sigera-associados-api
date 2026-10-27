using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Corretores;

namespace SigeraAssociados.Application.Interfaces.Corretores;

/// <summary>
/// Tabelas de apoio do corretor: agentes (T009AGENT) e gerentes de vendas
/// (T009GVED). Código omitido na inclusão: MAX + 1. Exclusão física,
/// recusada (Conflito) quando há corretor usando.
/// </summary>
public interface ITabelasApoioCorretorService
{
	Task<IReadOnlyList<AgenteDados>> ListarAgentesAsync(string? busca, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<AgenteDados>> IncluirAgenteAsync(AgenteDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<AgenteDados>> AlterarAgenteAsync(decimal sqAgente, AgenteDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<AgenteDados>> ExcluirAgenteAsync(decimal sqAgente, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<GerenteVendasDados>> ListarGerentesVendasAsync(string? busca, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<GerenteVendasDados>> IncluirGerenteVendasAsync(GerenteVendasDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<GerenteVendasDados>> AlterarGerenteVendasAsync(decimal sqGerenteVendas, GerenteVendasDados dados, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<GerenteVendasDados>> ExcluirGerenteVendasAsync(decimal sqGerenteVendas, CancellationToken cancellationToken = default);
}
