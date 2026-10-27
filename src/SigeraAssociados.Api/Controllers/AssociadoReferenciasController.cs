using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Referencias;
using SigeraAssociados.Application.Referencias;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Referências pessoais do associado (T000REFE): um associado pode ter várias.
/// Mesmas regras do associado: quem pode alterar o associado inclui e altera
/// as referências; excluir só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/referencias")]
[Authorize]
public sealed class AssociadoReferenciasController : ControllerBase
{
	private readonly IReferenciasAssociadoService _referencias;
	private readonly ILogger<AssociadoReferenciasController> _logger;

	public AssociadoReferenciasController(
		IReferenciasAssociadoService referencias,
		ILogger<AssociadoReferenciasController> logger)
	{
		_referencias = referencias;
		_logger = logger;
	}

	/// <summary>
	/// Lista as referências do associado.
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Referências, com telefones formatados (lista pode vir vazia).</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ReferenciaAssociadoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var referencias = await _referencias.ListarAsync(sqPesCli, cancellationToken);

		return referencias is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { sqPesCli, quantidade = referencias.Count, referencias });
	}

	/// <summary>
	/// Inclui uma referência no associado.
	/// </summary>
	/// <remarks>
	/// A sequência (`sqReferencia`) é gerada pela API (MAX + 1 da pessoa, até 9999).
	/// Obrigatórios: `nome` (até 50) e `telefone1`. Telefones com DDD + número
	/// (10 ou 11 números, com ou sem pontuação), gravados só com os números.
	/// `dataNascimento` e `telefone2` são opcionais.
	/// O mesmo nome e telefone 1 repetidos no associado são recusados (409).
	///
	///     POST /api/associados/{sqPesCli}/referencias
	///     {
	///       "nome": "MARIA DA SILVA",
	///       "dataNascimento": "1970-05-20",
	///       "telefone1": "(88) 99999-1234",
	///       "telefone2": "(88) 3521-1234"
	///     }
	/// </remarks>
	/// <response code="201">Referência incluída.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado não encontrado.</response>
	/// <response code="409">Referência já cadastrada no associado.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ReferenciaAssociadoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] ReferenciaAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _referencias.IncluirAsync(sqPesCli, dados, cancellationToken);
		Registrar(resultado.Status, "Referência incluída", sqPesCli, resultado.Item?.SqReferencia);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma referência do associado (envie todos os campos).
	/// </summary>
	/// <remarks>`dataNascimento` e `telefone2` omitidos ficam vazios.</remarks>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqReferencia">Sequência da referência.</param>
	/// <param name="dados">Dados da referência.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Referência alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado ou referência não encontrada.</response>
	/// <response code="409">Referência já cadastrada no associado.</response>
	[HttpPut("{sqReferencia:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ReferenciaAssociadoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqReferencia, [FromBody] ReferenciaAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _referencias.AlterarAsync(sqPesCli, sqReferencia, dados, cancellationToken);
		Registrar(resultado.Status, "Referência alterada", sqPesCli, sqReferencia);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma referência do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <response code="200">Referência excluída.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou referência não encontrada.</response>
	[HttpDelete("{sqReferencia:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqReferencia, CancellationToken cancellationToken)
	{
		var resultado = await _referencias.ExcluirAsync(sqPesCli, sqReferencia, cancellationToken);
		Registrar(resultado.Status, "Referência excluída", sqPesCli, sqReferencia);
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao, decimal sqPesCli, decimal? sqReferencia)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, referência {SqReferencia}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqReferencia,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
