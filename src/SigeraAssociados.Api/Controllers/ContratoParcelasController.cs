using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;
using SigeraAssociados.Application.Interfaces.Contratos;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Parcelas do contrato (T009PAGA). Quem alcança o contrato lista, inclui
/// e altera; excluir só Administradores e Gestores. A API grava só
/// vencimento e valor: os campos de pagamento ficam com as rotinas de baixa
/// do legado, e parcela paga, averbada ou em remessa não muda (409).
/// Cada gravação acerta a quantidade de parcelas do contrato.
/// </summary>
[ApiController]
[Route("api/contratos/{cdFil:decimal}/{sqCtr:decimal}/parcelas")]
[Authorize]
public sealed class ContratoParcelasController : ControllerBase
{
	private readonly IParcelasContratoService _parcelas;
	private readonly ILogger<ContratoParcelasController> _logger;

	public ContratoParcelasController(IParcelasContratoService parcelas, ILogger<ContratoParcelasController> logger)
	{
		_parcelas = parcelas;
		_logger = logger;
	}

	/// <summary>Parcelas do contrato, com totais.</summary>
	/// <response code="200">Parcelas na ordem do número.</response>
	/// <response code="404">Contrato não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(ResumoParcelas), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken)
	{
		var parcelas = await _parcelas.ListarAsync(cdFil, sqCtr, cancellationToken);

		return parcelas is null
			? this.Erro(StatusCodes.Status404NotFound, "Contrato não encontrado.")
			: Ok(parcelas);
	}

	/// <summary>
	/// Inclui uma parcela no fim do contrato (número = MAX + 1).
	/// </summary>
	/// <remarks>
	///     POST /api/contratos/{cdFil}/{sqCtr}/parcelas
	///     { "dataVencimento": "2027-12-10", "valor": 280.50 }
	/// </remarks>
	/// <response code="201">Parcela incluída.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Contrato não encontrado.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ParcelaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Incluir(decimal cdFil, decimal sqCtr, [FromBody] ParcelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _parcelas.IncluirAsync(cdFil, sqCtr, dados, Login(), cancellationToken);
		Registrar(resultado, "Parcela incluída", cdFil, sqCtr);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>Altera vencimento e valor de uma parcela não baixada.</summary>
	/// <response code="200">Parcela alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Contrato ou parcela não encontrados.</response>
	/// <response code="409">Parcela paga, averbada ou em remessa.</response>
	[HttpPut("{numeroParcela:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ParcelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal cdFil, decimal sqCtr, decimal numeroParcela, [FromBody] ParcelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _parcelas.AlterarAsync(cdFil, sqCtr, numeroParcela, dados, cancellationToken);
		Registrar(resultado, "Parcela alterada", cdFil, sqCtr);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma parcela não baixada (exclusão física). Administradores e Gestores.
	/// </summary>
	/// <remarks>A última parcela do contrato não pode ser excluída (409).</remarks>
	/// <response code="200">Parcela excluída.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Contrato ou parcela não encontrados.</response>
	/// <response code="409">Parcela baixada, ou única parcela do contrato.</response>
	[HttpDelete("{numeroParcela:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(typeof(ParcelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal cdFil, decimal sqCtr, decimal numeroParcela, CancellationToken cancellationToken)
	{
		var resultado = await _parcelas.ExcluirAsync(cdFil, sqCtr, numeroParcela, cancellationToken);
		Registrar(resultado, "Parcela excluída", cdFil, sqCtr);
		return this.Converter(resultado);
	}

	private string Login() =>
		User.FindFirstValue(ClaimTypes.Email)
		?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
		?? User.Identity?.Name
		?? "api";

	// Valor e vencimento vão para o log: mudança de parcela precisa de rastro.
	private void Registrar(ResultadoTabelaApoio<ParcelaDto> resultado, string operacao, decimal cdFil, decimal sqCtr)
	{
		if (resultado.Status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: filial {CdFil}, contrato {SqCtr}, parcela {NumeroParcela}, vencimento {Vencimento:yyyy-MM-dd}, valor {Valor}, por {Usuario}.",
				operacao, cdFil, sqCtr, resultado.Item?.NumeroParcela, resultado.Item?.DataVencimento, resultado.Item?.Valor, Login());
		}
	}
}
