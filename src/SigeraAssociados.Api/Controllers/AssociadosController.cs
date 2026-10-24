
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Interfaces.Associados;

namespace SigeraAssociados.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/associados")]
public sealed class AssociadosController : ControllerBase
{
	private readonly IAssociadosService _associadosService;
	private readonly ILogger<AssociadosController> _logger;

	public AssociadosController(
		IAssociadosService associadosService,
		ILogger<AssociadosController> logger)
	{
		_associadosService = associadosService;
		_logger = logger;
	}

	/// <summary>
	/// Lista os primeiros 10 associados.
	/// </summary>
	[HttpGet]
	public async Task<IActionResult> Listar(
		CancellationToken cancellationToken)
	{
		try
		{
			var associados = await _associadosService.ListarAsync(
				cancellationToken);

			return Ok(new
			{
				status = "sucesso",
				quantidade = associados.Count,
				associados
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Falha ao listar associados no sistema {SystemCode}.",
				Request.Headers["X-Sigera-Sistema"].ToString());

			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new
				{
					status = "falha_na_consulta",
					mensagem =
						"Não foi possível consultar os associados. Consulte o log da API."
				});
		}
	}

	/// <summary>
	/// Consulta um associado pela empresa e sequência da pessoa.
	/// </summary>
	[HttpGet("{cdEmp:decimal}/{sqPesCli:decimal}")]
	public async Task<IActionResult> ConsultarPorCodigo(
		decimal cdEmp,
		decimal sqPesCli,
		CancellationToken cancellationToken)
	{
		try
		{
			var associado =
				await _associadosService.ConsultarPorCodigoAsync(
					cdEmp,
					sqPesCli,
					cancellationToken);

			if (associado is null)
			{
				return NotFound(new
				{
					status = "nao_encontrado",
					mensagem =
						"Associado não encontrado para os códigos informados."
				});
			}

			return Ok(new
			{
				status = "sucesso",
				associado
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Falha ao consultar associado da empresa {Empresa}, pessoa {Pessoa}, sistema {SystemCode}.",
				cdEmp,
				sqPesCli,
				Request.Headers["X-Sigera-Sistema"].ToString());

			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new
				{
					status = "falha_na_consulta",
					mensagem =
						"Não foi possível consultar o associado. Consulte o log da API."
				});
		}
	}

	/// <summary>
	/// Consulta associados com filtros, paginação e ordenação.
	/// </summary>
	[HttpGet("consulta")]
	public async Task<IActionResult> ConsultarPaginado(
		[FromQuery] decimal? cdEmp,
		[FromQuery] string? nome,
		[FromQuery] decimal? cdSitCli,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 20,
		[FromQuery] string ordenarPor = "nome",
		[FromQuery] string direcao = "asc",
		CancellationToken cancellationToken = default)
	{
		if (pagina < 1)
		{
			return BadRequest(new
			{
				status = "parametro_invalido",
				mensagem = "A página deve ser maior ou igual a 1."
			});
		}

		if (tamanhoPagina < 1 || tamanhoPagina > 100)
		{
			return BadRequest(new
			{
				status = "parametro_invalido",
				mensagem = "O tamanho da página deve estar entre 1 e 100."
			});
		}

		var campoOrdenacao = ordenarPor.Trim().ToLowerInvariant();
		var sentido = direcao.Trim().ToLowerInvariant();

		var camposPermitidos = new[]
		{
			"nome",
			"cdemp",
			"sqpescli",
			"cdsitcli",
			"situacao",
			"dtcarteira"
		};

		if (!camposPermitidos.Contains(campoOrdenacao))
		{
			return BadRequest(new
			{
				status = "parametro_invalido",
				mensagem = "Campo de ordenação inválido.",
				camposPermitidos
			});
		}

		if (sentido != "asc" && sentido != "desc")
		{
			return BadRequest(new
			{
				status = "parametro_invalido",
				mensagem = "A direção deve ser 'asc' ou 'desc'."
			});
		}

		try
		{
			var request = new ConsultaAssociadosRequest
			{
				CdEmp = cdEmp,
				Nome = nome,
				CdSitCli = cdSitCli,
				Pagina = pagina,
				TamanhoPagina = tamanhoPagina,
				OrdenarPor = campoOrdenacao,
				Direcao = sentido
			};

			var resultado =
				await _associadosService.ConsultarPaginadoAsync(
					request,
					cancellationToken);

			return Ok(new
			{
				status = "sucesso",
				filtros = new
				{
					cdEmp,
					nome,
					cdSitCli
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
		catch (ArgumentException ex)
		{
			return BadRequest(new
			{
				status = "parametro_invalido",
				mensagem = ex.Message
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Falha ao consultar associados paginados no sistema {SystemCode}.",
				Request.Headers["X-Sigera-Sistema"].ToString());

			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new
				{
					status = "falha_na_consulta",
					mensagem =
						"Não foi possível consultar os associados. Consulte o log da API."
				});
		}
	}
}

