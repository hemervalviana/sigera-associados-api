using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Cadastros;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Pesquisa de filiais e corretores de um sistema, para escolher os valores
/// ao conceder acesso por filial ou por carteira de corretor
/// (POST /api/administracao/usuarios/{id}/acessos).
/// Cada sistema é uma base com uma única empresa: pesquisar por sistema
/// é pesquisar por empresa. Restrito a administradores, como a concessão.
/// </summary>
[ApiController]
[Route("api/administracao/sistemas/{sistema:int}")]
[Authorize(Roles = Grupos.PerfisAdministracao)]
public sealed class CadastrosSistemaController : ControllerBase
{
	private readonly ICadastrosSistemaService _cadastros;

	public CadastrosSistemaController(ICadastrosSistemaService cadastros)
	{
		_cadastros = cadastros;
	}

	/// <summary>
	/// Lista as filiais do sistema (tabela T000PARA).
	/// </summary>
	/// <remarks>
	/// Use o `cdFil` da filial escolhida na concessão de acesso e no filtro
	/// da pesquisa de corretores.
	///
	///     GET /api/administracao/sistemas/2/filiais
	/// </remarks>
	/// <param name="sistema">1 PROASP, 2 SONASP, 3 ABSP, 4 GSP.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Filiais ordenadas pelo código.</response>
	/// <response code="400">Sistema inválido.</response>
	/// <response code="403">Usuário não é administrador.</response>
	[HttpGet("filiais")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> ListarFiliais(
		int sistema,
		CancellationToken cancellationToken)
	{
		// Sistema inválido vira 400 pelo tratador global (DadosInvalidosException).
		var filiais = await _cadastros.ListarFiliaisAsync(sistema, cancellationToken);

		return Ok(new
		{
			sistema,
			quantidade = filiais.Count,
			filiais
		});
	}

	/// <summary>
	/// Pesquisa os corretores do sistema (tabela T009CRRT, nome de T000PESS).
	/// </summary>
	/// <remarks>
	/// Com `cdFil`, traz só os corretores dessa filial (use quando a filial já
	/// foi escolhida na concessão). `busca` procura pelo começo do nome ou pelo
	/// código do corretor.
	///
	/// Use o `sqPesCorretor` do corretor escolhido na concessão de acesso.
	///
	///     GET /api/administracao/sistemas/2/corretores?cdFil=1&amp;busca=MARIA
	/// </remarks>
	/// <param name="sistema">1 PROASP, 2 SONASP, 3 ABSP, 4 GSP.</param>
	/// <param name="cdFil">Filial (opcional).</param>
	/// <param name="busca">Começo do nome ou código do corretor (opcional).</param>
	/// <param name="pagina">Página, a partir de 1.</param>
	/// <param name="tamanhoPagina">Itens por página, de 1 a 200.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Página de corretores ordenada pelo nome.</response>
	/// <response code="400">Sistema, filial ou paginação inválidos.</response>
	/// <response code="403">Usuário não é administrador.</response>
	[HttpGet("corretores")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> ListarCorretores(
		int sistema,
		[FromQuery] decimal? cdFil,
		[FromQuery] string? busca,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 50,
		CancellationToken cancellationToken = default)
	{
		var resultado = await _cadastros.ListarCorretoresAsync(
			sistema,
			new ConsultaCorretoresRequest
			{
				CdFil = cdFil,
				Busca = busca,
				Pagina = pagina,
				TamanhoPagina = tamanhoPagina
			},
			cancellationToken);

		return Ok(new
		{
			sistema,
			cdFil,
			paginacao = new
			{
				pagina = resultado.Pagina,
				tamanhoPagina = resultado.TamanhoPagina,
				totalRegistros = resultado.TotalRegistros,
				totalPaginas = resultado.TotalPaginas
			},
			quantidade = resultado.Corretores.Count,
			corretores = resultado.Corretores
		});
	}
}
