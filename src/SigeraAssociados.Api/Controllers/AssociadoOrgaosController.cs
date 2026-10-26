using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Orgaos;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Órgãos em que o associado trabalha (vínculos em T000RPOR): um associado
/// servidor público pode estar lotado em um ou mais órgãos.
/// Mesmas regras do associado: quem pode alterar o associado inclui e
/// altera os vínculos; excluir vínculo só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/orgaos")]
[Authorize]
public sealed class AssociadoOrgaosController : ControllerBase
{
	private readonly IVinculosOrgaoService _vinculos;
	private readonly ILogger<AssociadoOrgaosController> _logger;

	public AssociadoOrgaosController(
		IVinculosOrgaoService vinculos,
		ILogger<AssociadoOrgaosController> logger)
	{
		_vinculos = vinculos;
		_logger = logger;
	}

	/// <summary>
	/// Lista os órgãos do associado, com lotação, situação, matrícula e salários.
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Vínculos do associado (lista pode vir vazia).</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<VinculoOrgaoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var vinculos = await _vinculos.ListarAsync(sqPesCli, cancellationToken);

		return vinculos is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { sqPesCli, quantidade = vinculos.Count, orgaos = vinculos });
	}

	/// <summary>
	/// Inclui o associado em um órgão.
	/// </summary>
	/// <remarks>
	/// A sequência do vínculo (`sqOrg`) é gerada pela API (MAX + 1 da pessoa).
	/// Obrigatório: `cdOrg` (GET /api/orgaos). Lotação, cargo e situação vêm de
	/// GET /api/orgaos/lotacoes, /api/orgaos/cargos e /api/orgaos/situacoes. O mesmo órgão com
	/// a mesma matrícula não se repete (409).
	///
	///     POST /api/associados/{sqPesCli}/orgaos
	///     {
	///       "cdOrg": 120,
	///       "cdLotacao": 35,
	///       "cdSituacao": 1,
	///       "matricula": "123456",
	///       "digitoMatricula": "7",
	///       "salario": 3500.00,
	///       "salarioLiquido": 2980.50
	///     }
	/// </remarks>
	/// <response code="201">Vínculo incluído.</response>
	/// <response code="400">Dados inválidos, ou órgão, lotação ou situação inexistente.</response>
	/// <response code="404">Associado não encontrado.</response>
	/// <response code="409">Mesmo órgão e matrícula já cadastrados.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(VinculoOrgaoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] VinculoOrgaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _vinculos.IncluirAsync(sqPesCli, dados, cancellationToken);
		Registrar(resultado.Status == StatusTabelaApoio.Sucesso, "Vínculo incluído", sqPesCli, resultado.Item?.SqOrg);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um vínculo do associado (envie todos os campos).
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqOrg">Sequência do vínculo.</param>
	/// <param name="dados">Dados do vínculo.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Vínculo alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado ou vínculo não encontrado.</response>
	/// <response code="409">Mesmo órgão e matrícula em outro vínculo.</response>
	[HttpPut("{sqOrg:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(VinculoOrgaoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqOrg, [FromBody] VinculoOrgaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _vinculos.AlterarAsync(sqPesCli, sqOrg, dados, cancellationToken);
		Registrar(resultado.Status == StatusTabelaApoio.Sucesso, "Vínculo alterado", sqPesCli, sqOrg);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um vínculo do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <response code="200">Vínculo excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou vínculo não encontrado.</response>
	/// <response code="409">Vínculo usado por outra tabela do sistema.</response>
	[HttpDelete("{sqOrg:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqOrg, CancellationToken cancellationToken)
	{
		var resultado = await _vinculos.ExcluirAsync(sqPesCli, sqOrg, cancellationToken);
		Registrar(resultado.Status == StatusTabelaApoio.Sucesso, "Vínculo excluído", sqPesCli, sqOrg);
		return this.Converter(resultado);
	}

	private void Registrar(bool sucesso, string operacao, decimal sqPesCli, decimal? sqOrg)
	{
		if (sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, vínculo {SqOrg}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqOrg,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
