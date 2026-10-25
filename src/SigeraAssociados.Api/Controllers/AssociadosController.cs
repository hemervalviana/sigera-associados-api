using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Interfaces.Associados;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Consultas de associados do sistema escolhido no cabeçalho X-Sigera-Sistema.
/// A validação dos parâmetros fica no AssociadosService (erros viram 400);
/// falhas inesperadas são tratadas pelo TratadorExcecoes (500, com log).
/// </summary>
[Authorize]
[ApiController]
[Route("api/associados")]
public sealed class AssociadosController : ControllerBase
{
	private readonly IAssociadosService _associadosService;

	public AssociadosController(IAssociadosService associadosService)
	{
		_associadosService = associadosService;
	}

	/// <summary>
	/// Lista os primeiros 10 associados.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Listar(CancellationToken cancellationToken)
	{
		var associados = await _associadosService.ListarAsync(cancellationToken);

		return Ok(new
		{
			status = "sucesso",
			quantidade = associados.Count,
			associados
		});
	}

	/// <summary>
	/// Consulta um associado pela empresa e sequência da pessoa.
	/// </summary>
	[HttpGet("{cdEmp:decimal}/{sqPesCli:decimal}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ConsultarPorCodigo(
		decimal cdEmp,
		decimal sqPesCli,
		CancellationToken cancellationToken)
	{
		var associado = await _associadosService.ConsultarPorCodigoAsync(
			cdEmp,
			sqPesCli,
			cancellationToken);

		if (associado is null)
		{
			return this.Erro(
				StatusCodes.Status404NotFound,
				"Associado não encontrado para os códigos informados.");
		}

		return Ok(new
		{
			status = "sucesso",
			associado
		});
	}

	/// <summary>
	/// Consulta associados com filtros, paginação e ordenação.
	/// nome: começo do nome (ex.: "MARIA" encontra "MARIA DA SILVA").
	/// cpf: CPF completo, com ou sem pontuação (busca exata; só pessoa física).
	/// ordenarPor: nome, cdemp, sqpescli, cdsitcli, situacao ou dtcarteira.
	/// direcao: asc ou desc. tamanhoPagina: 1 a 100.
	/// </summary>
	[HttpGet("consulta")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ConsultarPaginado(
		[FromQuery] decimal? cdEmp,
		[FromQuery] string? nome,
		[FromQuery] decimal? cdSitCli,
		[FromQuery] string? cpf,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 20,
		[FromQuery] string ordenarPor = "nome",
		[FromQuery] string direcao = "asc",
		CancellationToken cancellationToken = default)
	{
		var resultado = await _associadosService.ConsultarPaginadoAsync(
			new ConsultaAssociadosRequest
			{
				CdEmp = cdEmp,
				Nome = nome,
				CdSitCli = cdSitCli,
				Cpf = cpf,
				Pagina = pagina,
				TamanhoPagina = tamanhoPagina,
				OrdenarPor = ordenarPor,
				Direcao = direcao
			},
			cancellationToken);

		return Ok(new
		{
			status = "sucesso",
			filtros = new
			{
				cdEmp,
				nome,
				cdSitCli,
				// Não devolve o CPF pesquisado, só se o filtro foi usado.
				cpfInformado = !string.IsNullOrWhiteSpace(cpf)
			},
			paginacao = new
			{
				pagina = resultado.Pagina,
				tamanhoPagina = resultado.TamanhoPagina,
				totalRegistros = resultado.TotalRegistros,
				totalPaginas = resultado.TotalPaginas
			},
			ordenacao = new
			{
				campo = resultado.CampoOrdenacao,
				direcao = resultado.Direcao
			},
			quantidade = resultado.Quantidade,
			associados = resultado.Associados
		});
	}
}
