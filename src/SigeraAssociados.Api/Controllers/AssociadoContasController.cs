using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.ContasBancarias;
using SigeraAssociados.Application.Interfaces.ContasBancarias;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Contas bancárias do associado (T000CCOR): um associado pode ter uma ou várias.
/// Mesmas regras do associado: quem pode alterar o associado inclui e altera
/// as contas; excluir só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/contas")]
[Authorize]
public sealed class AssociadoContasController : ControllerBase
{
	private readonly IContasBancariasService _contas;
	private readonly ILogger<AssociadoContasController> _logger;

	public AssociadoContasController(
		IContasBancariasService contas,
		ILogger<AssociadoContasController> logger)
	{
		_contas = contas;
		_logger = logger;
	}

	/// <summary>
	/// Lista as contas bancárias do associado, com o nome do banco.
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Contas (lista pode vir vazia).</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ContaBancariaDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var contas = await _contas.ListarAsync(sqPesCli, cancellationToken);

		return contas is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { sqPesCli, quantidade = contas.Count, contas });
	}

	/// <summary>
	/// Inclui uma conta bancária no associado.
	/// </summary>
	/// <remarks>
	/// A sequência (`sqConta`) é gerada pela API (MAX + 1 da pessoa, até 99).
	/// Obrigatórios: `cdBanco` (GET /api/cadastros/bancos), `agencia` e
	/// `numeroConta`. A mesma conta (banco, agência e número) não se repete (409).
	///
	///     POST /api/associados/{sqPesCli}/contas
	///     {
	///       "cdBanco": 104,
	///       "agencia": 1234,
	///       "digitoAgencia": "5",
	///       "numeroConta": 123456,
	///       "digitoConta": "7",
	///       "operacao": "013",
	///       "cdTipoConta": 1,
	///       "titular": "MARIA DA SILVA",
	///       "chavePix": "maria@exemplo.com",
	///       "enviarOptanteCef": false
	///     }
	///
	/// Códigos fixos: `cdTipoConta` 0 = SEM CONTA, 1 = CONTA CORRENTE,
	/// 2 = POUPANÇA; `cdSituacao` 1 = ATIVA, 2 = INATIVA (omitida: 1);
	/// `enviarOptanteCef` true/false (gravado como 2 = SIM, 1 = NÃO).
	/// A resposta traz também `tipoConta` e `situacao` por extenso.
	/// </remarks>
	/// <response code="201">Conta incluída.</response>
	/// <response code="400">Dados inválidos ou banco inexistente.</response>
	/// <response code="404">Associado não encontrado.</response>
	/// <response code="409">Conta repetida.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ContaBancariaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] ContaBancariaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _contas.IncluirAsync(sqPesCli, dados, cancellationToken);
		Registrar(resultado.Status, "Conta incluída", sqPesCli, resultado.Item?.SqConta);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma conta bancária do associado (envie todos os campos).
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqConta">Sequência da conta.</param>
	/// <param name="dados">Dados da conta.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Conta alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado ou conta não encontrada.</response>
	/// <response code="409">Conta repetida.</response>
	[HttpPut("{sqConta:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ContaBancariaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqConta, [FromBody] ContaBancariaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _contas.AlterarAsync(sqPesCli, sqConta, dados, cancellationToken);
		Registrar(resultado.Status, "Conta alterada", sqPesCli, sqConta);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma conta bancária do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <response code="200">Conta excluída.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou conta não encontrada.</response>
	/// <response code="409">Conta usada por outra tabela do sistema.</response>
	[HttpDelete("{sqConta:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqConta, CancellationToken cancellationToken)
	{
		var resultado = await _contas.ExcluirAsync(sqPesCli, sqConta, cancellationToken);
		Registrar(resultado.Status, "Conta excluída", sqPesCli, sqConta);
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao, decimal sqPesCli, decimal? sqConta)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, conta {SqConta}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqConta,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
