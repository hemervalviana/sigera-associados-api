using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Creditos;
using SigeraAssociados.Application.Interfaces.Creditos;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Créditos do associado (T009EXTRA): um associado pode ter vários.
/// Mesmas regras do associado: quem pode alterar o associado inclui e altera
/// os créditos; excluir só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/creditos")]
[Authorize]
public sealed class AssociadoCreditosController : ControllerBase
{
	private readonly ICreditosAssociadoService _creditos;
	private readonly ILogger<AssociadoCreditosController> _logger;

	public AssociadoCreditosController(
		ICreditosAssociadoService creditos,
		ILogger<AssociadoCreditosController> logger)
	{
		_creditos = creditos;
		_logger = logger;
	}

	/// <summary>
	/// Lista os créditos do associado (o mais recente primeiro).
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Créditos com descrições, soma dos valores e dos saldos.</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CreditoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var creditos = await _creditos.ListarAsync(sqPesCli, cancellationToken);

		return creditos is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new
			{
				sqPesCli,
				quantidade = creditos.Count,
				totalValor = creditos.Sum(c => c.Valor),
				totalSaldo = creditos.Sum(c => c.Saldo),
				creditos
			});
	}

	/// <summary>
	/// Inclui um crédito no associado.
	/// </summary>
	/// <remarks>
	/// A API gera a sequência (`sqCredito` = SQ_PGT, MAX + 1 do associado) e o
	/// saldo, como o legado:
	/// - forma de pagamento que gera saldo (`geraSaldo` em
	///   GET /api/cadastros/formas-pagamento): saldo = valor;
	/// - forma que não gera saldo: saldo = 0.
	///
	/// `valor` é obrigatório e precisa ser maior que zero (senão, 400).
	/// O saldo é consumido depois por outras rotinas, até chegar a zero.
	///
	/// Omitidos: `dataCredito` = hoje; `tipoBaixa` = 4 (CRÉDITO).
	/// `tipoBaixa`: 1 = AUTOMÁTICA, 2 = MANUAL, 4 = CRÉDITO.
	/// Contrato e averbação exigem a filial; a averbação exige também o convênio.
	///
	///     POST /api/associados/{sqPesCli}/creditos
	///     {
	///       "dataCredito": "2026-10-27",
	///       "valor": 150.00,
	///       "cdFormaPagamento": 1,
	///       "observacao": "CREDITO POR PAGAMENTO EM DUPLICIDADE",
	///       "cdFil": 1,
	///       "cdProduto": 1
	///     }
	/// </remarks>
	/// <response code="201">Crédito incluído.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CreditoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] CreditoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _creditos.IncluirAsync(sqPesCli, dados, cancellationToken);
		Registrar(resultado, "Crédito incluído", sqPesCli, resultado.Item?.SqCredito);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um crédito do associado (envie todos os campos).
	/// </summary>
	/// <remarks>
	/// `valor` e `cdFormaPagamento` só podem mudar enquanto o saldo não foi
	/// utilizado (saldo = valor, `saldoUtilizado: false` na listagem); aí o
	/// saldo é recalculado. Com o saldo já utilizado, mudar um dos dois dá 409;
	/// os outros campos podem ser alterados sempre.
	///
	/// `valor`, quando enviado, precisa ser maior que zero. `dataCredito`,
	/// `valor` e `tipoBaixa` omitidos mantêm o que está gravado.
	/// </remarks>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqCredito">Sequência do crédito (SQ_PGT).</param>
	/// <param name="dados">Dados do crédito.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Crédito alterado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="404">Associado ou crédito não encontrado.</response>
	/// <response code="409">Saldo já utilizado: valor e forma de pagamento travados.</response>
	[HttpPut("{sqCredito:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CreditoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqCredito, [FromBody] CreditoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _creditos.AlterarAsync(sqPesCli, sqCredito, dados, cancellationToken);
		Registrar(resultado, "Crédito alterado", sqPesCli, sqCredito);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um crédito do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Regra do legado: só exclui quando o valor do pagamento é igual ao saldo
	/// (`podeExcluir` na listagem). Senão, 409.
	/// </remarks>
	/// <response code="200">Crédito excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou crédito não encontrado.</response>
	/// <response code="409">Valor do pagamento diferente do saldo.</response>
	[HttpDelete("{sqCredito:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqCredito, CancellationToken cancellationToken)
	{
		var resultado = await _creditos.ExcluirAsync(sqPesCli, sqCredito, cancellationToken);
		Registrar(resultado, "Crédito excluído", sqPesCli, sqCredito);
		return this.Converter(resultado);
	}

	// Valor e saldo vão para o log: mudança de crédito precisa de rastro.
	private void Registrar(ResultadoTabelaApoio<CreditoDto> resultado, string operacao, decimal sqPesCli, decimal? sqCredito)
	{
		if (resultado.Status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, crédito {SqCredito}, valor {Valor}, saldo {Saldo}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqCredito,
				resultado.Item?.Valor,
				resultado.Item?.Saldo,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
