using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Financeiro;
using SigeraAssociados.Application.Interfaces.Financeiro;
using SigeraAssociados.Application.Usuarios;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Convênios (T009CONV). Sistema pelo cabeçalho X-Sigera-Sistema.
/// Consultar: qualquer usuário com acesso ao sistema.
/// Incluir, alterar e excluir: somente administradores.
/// </summary>
[ApiController]
[Route("api/convenios")]
[Authorize]
public sealed class ConveniosController : ControllerBase
{
	private readonly ICadastrosFinanceirosService _financeiro;
	private readonly SigeraSystemContext _sistema;
	private readonly ILogger<ConveniosController> _logger;

	public ConveniosController(
		ICadastrosFinanceirosService financeiro,
		SigeraSystemContext sistema,
		ILogger<ConveniosController> logger)
	{
		_financeiro = financeiro;
		_sistema = sistema;
		_logger = logger;
	}

	// O middleware de sistema já validou o cabeçalho e o acesso.
	private int Sistema => _sistema.SystemCode
		?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

	/// <summary>
	/// Lista os convênios.
	/// </summary>
	/// <param name="busca">Começo do nome ou o código (opcional).</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Convênios ordenados pelo nome.</response>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Listar([FromQuery] string? busca, CancellationToken cancellationToken)
	{
		var convenios = await _financeiro.ListarConveniosAsync(Sistema, busca, cancellationToken);

		return Ok(new { quantidade = convenios.Count, convenios });
	}

	/// <summary>
	/// Dados completos de um convênio.
	/// </summary>
	/// <response code="200">Convênio.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpGet("{cdCvn:decimal}")]
	[ProducesResponseType(typeof(ConvenioDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Obter(decimal cdCvn, CancellationToken cancellationToken)
	{
		var convenio = await _financeiro.ObterConvenioAsync(Sistema, cdCvn, cancellationToken);

		return convenio is null
			? this.Erro(StatusCodes.Status404NotFound, "Convênio não encontrado.")
			: Ok(new { convenio });
	}

	/// <summary>
	/// Inclui um convênio. Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdCvn` omitido: MAX + 1 (até 999). Obrigatório: `nome` (até 35).
	/// Filial, banco e conta corrente (T009CONT) são conferidos nas tabelas.
	///
	///     POST /api/convenios
	///     {
	///       "nome": "CONSIGNADO SEDUC",
	///       "numero": "123456",
	///       "cdFil": 1,
	///       "cdBanco": 1,
	///       "diretorioRemessa": "C:\\CONVENIOS\\REMESSA",
	///       "diretorioRetorno": "C:\\CONVENIOS\\RETORNO",
	///       "extensaoArquivo": "TXT",
	///       "cdSituacao": 1
	///     }
	/// </remarks>
	/// <response code="201">Convênio cadastrado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ConvenioDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir([FromBody] ConvenioDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.IncluirConvenioAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Convênio incluído", resultado.Item?.CdCvn);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um convênio (envie todos os campos). Somente administradores.
	/// </summary>
	/// <response code="200">Convênio alterado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("{cdCvn:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ConvenioDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Alterar(decimal cdCvn, [FromBody] ConvenioDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.AlterarConvenioAsync(Sistema, cdCvn, dados, cancellationToken);
		Registrar(resultado.Status, "Convênio alterado", cdCvn);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um convênio. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver créditos ou averbações (T009RBSR) no convênio.</remarks>
	/// <response code="200">Convênio excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Convênio em uso.</response>
	[HttpDelete("{cdCvn:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal cdCvn, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.ExcluirConvenioAsync(Sistema, cdCvn, cancellationToken);
		Registrar(resultado.Status, "Convênio excluído", cdCvn);
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao, decimal? cdCvn)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: convênio {CdCvn} no sistema {Sistema} por {Usuario}.",
				operacao,
				cdCvn,
				Sistema,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
