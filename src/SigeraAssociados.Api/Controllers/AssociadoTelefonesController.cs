using Microsoft.AspNetCore.Authorization;
using SigeraAssociados.Application.Pessoas;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Telefones;
using SigeraAssociados.Application.Telefones;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Telefones do associado (T000TELE): um associado pode ter vários.
/// Mesmas regras do associado: quem pode alterar o associado inclui e altera
/// os telefones; excluir só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/telefones")]
[Authorize]
public sealed class AssociadoTelefonesController : ControllerBase
{
	private readonly ITelefonesAssociadoService _telefones;
	private readonly ILogger<AssociadoTelefonesController> _logger;

	public AssociadoTelefonesController(
		ITelefonesAssociadoService telefones,
		ILogger<AssociadoTelefonesController> logger)
	{
		_telefones = telefones;
		_logger = logger;
	}

	/// <summary>
	/// Lista os telefones do associado.
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Telefones, com tipo e número formatado (lista pode vir vazia).</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<TelefoneAssociadoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var telefones = await _telefones.ListarAsync(TitularCadastro.Associado, sqPesCli, cancellationToken);

		return telefones is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { sqPesCli, quantidade = telefones.Count, telefones });
	}

	/// <summary>
	/// Inclui um telefone no associado.
	/// </summary>
	/// <remarks>
	/// A sequência (`sqTelefone`) é gerada pela API (MAX + 1 da empresa).
	/// Obrigatórios: `ddd` (11 a 99) e `numero` (8 ou 9 números, com ou sem traço;
	/// gravado só com os números). `cdTipo` é opcional (GET /api/cadastros/tipos-telefone).
	/// O mesmo DDD e número repetido no associado é recusado (409).
	///
	///     POST /api/associados/{sqPesCli}/telefones
	///     {
	///       "ddd": 88,
	///       "numero": "99999-1234",
	///       "cdTipo": 2
	///     }
	/// </remarks>
	/// <response code="201">Telefone incluído.</response>
	/// <response code="400">Dados inválidos ou tipo inexistente.</response>
	/// <response code="404">Associado não encontrado.</response>
	/// <response code="409">Telefone já cadastrado no associado.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(TelefoneAssociadoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] TelefoneAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _telefones.IncluirAsync(TitularCadastro.Associado, sqPesCli, dados, cancellationToken);
		Registrar(resultado.Status, "Telefone incluído", sqPesCli, resultado.Item?.SqTelefone);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um telefone do associado (envie todos os campos).
	/// </summary>
	/// <remarks>`cdTipo` omitido deixa o telefone sem tipo.</remarks>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqTelefone">Sequência do telefone.</param>
	/// <param name="dados">Dados do telefone.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Telefone alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado ou telefone não encontrado.</response>
	/// <response code="409">Telefone já cadastrado no associado.</response>
	[HttpPut("{sqTelefone:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(TelefoneAssociadoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqTelefone, [FromBody] TelefoneAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _telefones.AlterarAsync(TitularCadastro.Associado, sqPesCli, sqTelefone, dados, cancellationToken);
		Registrar(resultado.Status, "Telefone alterado", sqPesCli, sqTelefone);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um telefone do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <response code="200">Telefone excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou telefone não encontrado.</response>
	[HttpDelete("{sqTelefone:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqTelefone, CancellationToken cancellationToken)
	{
		var resultado = await _telefones.ExcluirAsync(TitularCadastro.Associado, sqPesCli, sqTelefone, cancellationToken);
		Registrar(resultado.Status, "Telefone excluído", sqPesCli, sqTelefone);
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao, decimal sqPesCli, decimal? sqTelefone)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, telefone {SqTelefone}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqTelefone,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
