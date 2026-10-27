using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Pix;
using SigeraAssociados.Application.Pix;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Chaves Pix do associado (T009PIX): um associado pode ter várias.
/// Mesmas regras do associado: quem pode alterar o associado inclui e altera
/// as chaves; excluir só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/pix")]
[Authorize]
public sealed class AssociadoPixController : ControllerBase
{
	private readonly IChavesPixService _pix;
	private readonly ILogger<AssociadoPixController> _logger;

	public AssociadoPixController(IChavesPixService pix, ILogger<AssociadoPixController> logger)
	{
		_pix = pix;
		_logger = logger;
	}

	/// <summary>
	/// Lista as chaves Pix do associado.
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Chaves, com tipo e chave formatada (lista pode vir vazia).</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ChavePixDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var chaves = await _pix.ListarAsync(sqPesCli, cancellationToken);

		return chaves is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { sqPesCli, quantidade = chaves.Count, chaves });
	}

	/// <summary>
	/// Inclui uma chave Pix no associado.
	/// </summary>
	/// <remarks>
	/// A sequência (`sqPix`) é gerada pela API (MAX + 1 da pessoa, até 99).
	///
	/// `cdTipo` (valores fixos do legado) e o formato da `chave`:
	/// - **1 = CPF/CNPJ**: 11 ou 14 números, com dígitos verificadores válidos; gravado só com números.
	/// - **2 = CELULAR**: DDD + 9 números, com ou sem +55 e pontuação; gravado como DDD + número.
	/// - **3 = E-MAIL**: endereço válido, até 77 caracteres; gravado em minúsculas.
	/// - **4 = ALEATÓRIA**: 36 caracteres (UUID); gravada em minúsculas.
	///
	/// A mesma chave repetida no associado é recusada (409).
	///
	///     POST /api/associados/{sqPesCli}/pix
	///     { "cdTipo": 2, "chave": "+55 (88) 99999-1234" }
	/// </remarks>
	/// <response code="201">Chave incluída.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado não encontrado.</response>
	/// <response code="409">Chave já cadastrada no associado.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ChavePixDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] ChavePixDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _pix.IncluirAsync(sqPesCli, dados, cancellationToken);
		Registrar(resultado.Status, "Chave Pix incluída", sqPesCli, resultado.Item?.SqPix);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma chave Pix do associado (envie todos os campos).
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqPix">Sequência da chave.</param>
	/// <param name="dados">Dados da chave.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Chave alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado ou chave não encontrada.</response>
	/// <response code="409">Chave já cadastrada no associado.</response>
	[HttpPut("{sqPix:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ChavePixDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqPix, [FromBody] ChavePixDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _pix.AlterarAsync(sqPesCli, sqPix, dados, cancellationToken);
		Registrar(resultado.Status, "Chave Pix alterada", sqPesCli, sqPix);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma chave Pix do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <response code="200">Chave excluída.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou chave não encontrada.</response>
	[HttpDelete("{sqPix:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqPix, CancellationToken cancellationToken)
	{
		var resultado = await _pix.ExcluirAsync(sqPesCli, sqPix, cancellationToken);
		Registrar(resultado.Status, "Chave Pix excluída", sqPesCli, sqPix);
		return this.Converter(resultado);
	}

	// A chave não vai para o log (dado pessoal): só a sequência.
	private void Registrar(StatusTabelaApoio status, string operacao, decimal sqPesCli, decimal? sqPix)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, chave {SqPix}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqPix,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
