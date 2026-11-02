using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;

namespace SigeraAssociados.Application.Interfaces.Contratos;

/// <summary>
/// Gravação das tabelas de apoio do contrato: situações (T009SCTR), carências (T009CARE), origens
/// do recurso (T009OREC), produtores (T009PROD) e índices (T009INDI). A
/// consulta fica em IContratosService. Só administradores (controle no
/// controller). Exclusão recusada (Conflito) quando há contrato usando.
/// </summary>
public interface ITabelasApoioContratoService
{
	Task<ResultadoTabelaApoio<SituacaoContratoApoioDados>> IncluirSituacaoAsync(SituacaoContratoApoioDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<SituacaoContratoApoioDados>> AlterarSituacaoAsync(decimal cdSituacao, SituacaoContratoApoioDados dados, CancellationToken cancellationToken = default);

	/// <summary>Recusada (Conflito) para as situações fixas da API e para as usadas por contratos.</summary>
	Task<ResultadoTabelaApoio<SituacaoContratoApoioDados>> ExcluirSituacaoAsync(decimal cdSituacao, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<CarenciaDados>> IncluirCarenciaAsync(CarenciaDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<CarenciaDados>> AlterarCarenciaAsync(decimal cdCarencia, CarenciaDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<CarenciaDados>> ExcluirCarenciaAsync(decimal cdCarencia, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<OrigemRecursoDados>> IncluirOrigemRecursoAsync(OrigemRecursoDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<OrigemRecursoDados>> AlterarOrigemRecursoAsync(decimal cdOrigemRecurso, OrigemRecursoDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<OrigemRecursoDados>> ExcluirOrigemRecursoAsync(decimal cdOrigemRecurso, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<ProdutorDados>> IncluirProdutorAsync(ProdutorDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<ProdutorDados>> AlterarProdutorAsync(decimal sqProdutor, ProdutorDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<ProdutorDados>> ExcluirProdutorAsync(decimal sqProdutor, CancellationToken cancellationToken = default);

	Task<ResultadoTabelaApoio<IndiceContratoDto>> IncluirIndiceAsync(IndiceDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<IndiceContratoDto>> AlterarIndiceAsync(decimal cdCarencia, decimal quantidadeParcelas, IndiceDados dados, CancellationToken cancellationToken = default);
	Task<ResultadoTabelaApoio<IndiceContratoDto>> ExcluirIndiceAsync(decimal cdCarencia, decimal quantidadeParcelas, CancellationToken cancellationToken = default);
}
