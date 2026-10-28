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
/// Parcelas do contrato (T009PAGA), com as regras da aba "Parcela" do
/// legado (o_tc_t009paga). Quem alcança o contrato lista, inclui e altera
/// (inclusive a baixa manual: data e valor pago). Excluir: parcela não paga,
/// Administradores e Gestores; parcela paga, Administradores e Financeiro.
/// Contrato quitado (situação 9) trava as parcelas (409). Cada gravação
/// acerta a quantidade de parcelas do contrato.
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
	/// Com `valorPago` maior que zero (e `dataPagamento`), a parcela já nasce
	/// com baixa manual. Ver as regras de pagamento na alteração.
	///
	///     POST /api/contratos/{cdFil}/{sqCtr}/parcelas
	///     { "dataVencimento": "2027-12-10", "valor": 280.50 }
	/// </remarks>
	/// <response code="201">Parcela incluída.</response>
	/// <response code="400">Dados inválidos ou saldo diferente de zero com o parâmetro 1 ligado.</response>
	/// <response code="404">Contrato não encontrado.</response>
	/// <response code="409">Contrato quitado.</response>
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

	/// <summary>
	/// Altera a parcela (envie a parcela inteira), inclusive a baixa manual.
	/// </summary>
	/// <remarks>
	/// Regras do legado:
	/// - `valorPago` maior que zero exige `dataPagamento`, e o contrário;
	/// - mudar o valor pago faz a baixa manual: saldo = pago - valor, tipo de
	///   baixa 1 (MANUAL), forma de pagamento 5 (OUTROS);
	/// - `valorPago` nulo ou zero desfaz o pagamento: saldo = -valor, tipo de
	///   baixa 3 (SEM BAIXA), forma nula;
	/// - mudar só o valor da parcela recalcula o saldo e mantém a baixa
	///   (inclusive a automática), então reenvie `valorPago` e `dataPagamento`
	///   atuais para conservar o pagamento;
	/// - com o parâmetro 1 da filial ligado (T009PARM, TP_FLG = 'S'), parcela
	///   paga precisa ficar com saldo zero (pago = valor).
	///
	///     PUT /api/contratos/{cdFil}/{sqCtr}/parcelas/{numeroParcela}
	///     { "dataVencimento": "2027-12-10", "valor": 280.50, "dataPagamento": "2027-12-08", "valorPago": 280.50 }
	/// </remarks>
	/// <response code="200">Parcela alterada.</response>
	/// <response code="400">Dados inválidos ou saldo diferente de zero com o parâmetro 1 ligado.</response>
	/// <response code="404">Contrato ou parcela não encontrados.</response>
	/// <response code="409">Contrato quitado.</response>
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
	/// Exclui uma parcela (exclusão física).
	/// </summary>
	/// <remarks>
	/// Parcela não paga: Administradores e Gestores. Parcela paga (tipo de
	/// baixa diferente de 3): Administradores e Financeiro, como no legado.
	/// A última parcela do contrato não pode ser excluída (409).
	/// </remarks>
	/// <response code="200">Parcela excluída.</response>
	/// <response code="403">Grupo sem permissão para esta parcela.</response>
	/// <response code="404">Contrato ou parcela não encontrados.</response>
	/// <response code="409">Contrato quitado, ou única parcela do contrato.</response>
	[HttpDelete("{numeroParcela:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoParcelas)]
	[ProducesResponseType(typeof(ParcelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal cdFil, decimal sqCtr, decimal numeroParcela, CancellationToken cancellationToken)
	{
		var resultado = await _parcelas.ExcluirAsync(
			cdFil,
			sqCtr,
			numeroParcela,
			podeExcluirPaga: Grupos.PodemExcluirParcelaPaga.Any(User.IsInRole),
			podeExcluirNaoPaga: Grupos.PodemExcluirParcelaNaoPaga.Any(User.IsInRole),
			cancellationToken);
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
