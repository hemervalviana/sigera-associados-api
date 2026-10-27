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
/// Tabelas de apoio do crédito do associado: formas de pagamento (T009FRPG)
/// e produtos (T009PRDT). Sistema pelo cabeçalho X-Sigera-Sistema.
/// Consultar: qualquer usuário com acesso ao sistema.
/// Incluir, alterar e excluir: somente administradores.
/// Exclusão física, recusada (409) quando o registro está em uso.
/// </summary>
[ApiController]
[Route("api/cadastros")]
[Authorize]
public sealed class CadastrosFinanceirosController : ControllerBase
{
	private readonly ICadastrosFinanceirosService _financeiro;
	private readonly SigeraSystemContext _sistema;
	private readonly ILogger<CadastrosFinanceirosController> _logger;

	public CadastrosFinanceirosController(
		ICadastrosFinanceirosService financeiro,
		SigeraSystemContext sistema,
		ILogger<CadastrosFinanceirosController> logger)
	{
		_financeiro = financeiro;
		_sistema = sistema;
		_logger = logger;
	}

	// O middleware de sistema já validou o cabeçalho e o acesso.
	private int Sistema => _sistema.SystemCode
		?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

	// ===================== Formas de pagamento =====================

	/// <summary>
	/// Formas de pagamento (T009FRPG), para o campo cdFormaPagamento do crédito.
	/// </summary>
	/// <response code="200">Formas ordenadas pelo código, com geraSaldo.</response>
	[HttpGet("formas-pagamento")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> FormasPagamento(CancellationToken cancellationToken)
	{
		var formas = await _financeiro.ListarFormasPagamentoAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = formas.Count, formasPagamento = formas });
	}

	/// <summary>
	/// Inclui uma forma de pagamento. Somente administradores.
	/// </summary>
	/// <remarks>
	/// `codigo` omitido: MAX + 1 (até 99). `geraSaldo`: no crédito do associado,
	/// a forma que gera saldo deixa o saldo igual ao valor; a que não gera, zerado.
	///
	///     POST /api/cadastros/formas-pagamento
	///     { "descricao": "DINHEIRO", "geraSaldo": true }
	/// </remarks>
	/// <response code="201">Forma cadastrada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("formas-pagamento")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(FormaPagamentoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirFormaPagamento([FromBody] FormaPagamentoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.IncluirFormaPagamentoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Forma de pagamento incluída", resultado.Item?.Codigo);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma forma de pagamento. Somente administradores.
	/// </summary>
	/// <remarks>
	/// Mudar `geraSaldo` não recalcula os créditos já gravados.
	/// </remarks>
	/// <response code="200">Forma alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrada.</response>
	[HttpPut("formas-pagamento/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(FormaPagamentoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarFormaPagamento(decimal codigo, [FromBody] FormaPagamentoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.AlterarFormaPagamentoAsync(Sistema, codigo, dados, cancellationToken);
		Registrar(resultado.Status, "Forma de pagamento alterada", codigo);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma forma de pagamento. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver créditos com a forma.</remarks>
	/// <response code="200">Forma excluída.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrada.</response>
	/// <response code="409">Forma em uso.</response>
	[HttpDelete("formas-pagamento/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirFormaPagamento(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.ExcluirFormaPagamentoAsync(Sistema, codigo, cancellationToken);
		Registrar(resultado.Status, "Forma de pagamento excluída", codigo);
		return this.Converter(resultado);
	}

	// ===================== Produtos =====================

	/// <summary>
	/// Produtos (T009PRDT), para o campo cdProduto do crédito.
	/// </summary>
	/// <response code="200">Produtos ordenados pelo código.</response>
	[HttpGet("produtos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Produtos(CancellationToken cancellationToken)
	{
		var produtos = await _financeiro.ListarProdutosAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = produtos.Count, produtos });
	}

	/// <summary>
	/// Inclui um produto. Somente administradores.
	/// </summary>
	/// <remarks>
	/// `codigo` omitido: MAX + 1 (até 99). `descricao` até 100.
	///
	///     POST /api/cadastros/produtos
	///     { "descricao": "PLANO FAMILIAR" }
	/// </remarks>
	/// <response code="201">Produto cadastrado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("produtos")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ProdutoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirProduto([FromBody] ProdutoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.IncluirProdutoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Produto incluído", resultado.Item?.Codigo);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera a descrição de um produto. Somente administradores.
	/// </summary>
	/// <response code="200">Produto alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("produtos/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ProdutoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarProduto(decimal codigo, [FromBody] ProdutoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.AlterarProdutoAsync(Sistema, codigo, dados, cancellationToken);
		Registrar(resultado.Status, "Produto alterado", codigo);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um produto. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver créditos com o produto.</remarks>
	/// <response code="200">Produto excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Produto em uso.</response>
	[HttpDelete("produtos/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirProduto(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _financeiro.ExcluirProdutoAsync(Sistema, codigo, cancellationToken);
		Registrar(resultado.Status, "Produto excluído", codigo);
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao, decimal? codigo)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: código {Codigo} no sistema {Sistema} por {Usuario}.",
				operacao,
				codigo,
				Sistema,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
