using Microsoft.AspNetCore.Authorization;
using SigeraAssociados.Application.Pessoas;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Enderecos;
using SigeraAssociados.Application.Interfaces.Enderecos;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Endereços do associado (T000ENDE): um associado pode ter um ou mais,
/// e sempre um deles é o principal.
/// Mesmas regras do associado: quem pode alterar o associado inclui e altera
/// os endereços; excluir só Administradores e Gestores.
/// Associado fora dos seus acessos: 404.
/// </summary>
[ApiController]
[Route("api/associados/{sqPesCli:decimal}/enderecos")]
[Authorize]
public sealed class AssociadoEnderecosController : ControllerBase
{
	private readonly IEnderecosAssociadoService _enderecos;
	private readonly ILogger<AssociadoEnderecosController> _logger;

	public AssociadoEnderecosController(
		IEnderecosAssociadoService enderecos,
		ILogger<AssociadoEnderecosController> logger)
	{
		_enderecos = enderecos;
		_logger = logger;
	}

	/// <summary>
	/// Lista os endereços do associado (o principal primeiro).
	/// </summary>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Endereços, com tipo, município e UF (lista pode vir vazia).</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<EnderecoAssociadoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Listar(decimal sqPesCli, CancellationToken cancellationToken)
	{
		var enderecos = await _enderecos.ListarAsync(TitularCadastro.Associado, sqPesCli, cancellationToken);

		return enderecos is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { sqPesCli, quantidade = enderecos.Count, enderecos });
	}

	/// <summary>
	/// Inclui um endereço no associado.
	/// </summary>
	/// <remarks>
	/// A sequência (`sqEndereco`) é gerada pela API (MAX + 1 da pessoa, até 99).
	/// Obrigatórios: `cdTipo` (GET /api/cadastros/tipos-endereco), `logradouro`
	/// e `cdMunicipio` (GET /api/cadastros/municipios).
	///
	/// `principal`: o primeiro endereço é sempre o principal. Ao incluir outro
	/// com `principal: true`, os demais deixam de ser.
	///
	///     POST /api/associados/{sqPesCli}/enderecos
	///     {
	///       "cdTipo": 1,
	///       "logradouro": "RUA SAO PEDRO",
	///       "numero": 120,
	///       "complemento": "APTO 201",
	///       "bairro": "CENTRO",
	///       "cep": "63010-000",
	///       "cdMunicipio": 2307304,
	///       "principal": true
	///     }
	/// </remarks>
	/// <response code="201">Endereço incluído.</response>
	/// <response code="400">Dados inválidos, ou tipo ou município inexistente.</response>
	/// <response code="404">Associado não encontrado.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(EnderecoAssociadoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Incluir(decimal sqPesCli, [FromBody] EnderecoAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _enderecos.IncluirAsync(TitularCadastro.Associado, sqPesCli, dados, cancellationToken);
		Registrar(resultado.Status, "Endereço incluído", sqPesCli, resultado.Item?.SqEndereco);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um endereço do associado (envie todos os campos).
	/// </summary>
	/// <remarks>
	/// `principal: true` torna este o principal e desmarca os outros.
	/// Omitido, mantém como estava. Para trocar o principal, marque o novo
	/// endereço como principal (não é possível só desmarcar).
	/// </remarks>
	/// <param name="sqPesCli">Código do associado.</param>
	/// <param name="sqEndereco">Sequência do endereço.</param>
	/// <param name="dados">Dados do endereço.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Endereço alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="404">Associado ou endereço não encontrado.</response>
	[HttpPut("{sqEndereco:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(EnderecoAssociadoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Alterar(decimal sqPesCli, decimal sqEndereco, [FromBody] EnderecoAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _enderecos.AlterarAsync(TitularCadastro.Associado, sqPesCli, sqEndereco, dados, cancellationToken);
		Registrar(resultado.Status, "Endereço alterado", sqPesCli, sqEndereco);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um endereço do associado (exclusão física). Somente Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Se era o principal, o endereço de menor sequência que sobrar passa a ser o principal.
	/// </remarks>
	/// <response code="200">Endereço excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Associado ou endereço não encontrado.</response>
	[HttpDelete("{sqEndereco:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(decimal sqPesCli, decimal sqEndereco, CancellationToken cancellationToken)
	{
		var resultado = await _enderecos.ExcluirAsync(TitularCadastro.Associado, sqPesCli, sqEndereco, cancellationToken);
		Registrar(resultado.Status, "Endereço excluído", sqPesCli, sqEndereco);
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao, decimal sqPesCli, decimal? sqEndereco)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: associado {SqPesCli}, endereço {SqEndereco}, por {Usuario}.",
				operacao,
				sqPesCli,
				sqEndereco,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
