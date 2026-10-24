using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Api.Services;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Diagnóstico técnico dos bancos de negócio.
/// Restrito a administradores, pois expõe servidor, banco e dados de pessoas.
/// </summary>
[Authorize(Roles = Grupos.PerfisAdministracao)]
[ApiController]
[Route("api/diagnostico")]
public sealed class DiagnosticController : ControllerBase
{
	private readonly SigeraDatabaseService _databaseService;
	private readonly ILogger<DiagnosticController> _logger;

	public DiagnosticController(
		SigeraDatabaseService databaseService,
		ILogger<DiagnosticController> logger)
	{
		_databaseService = databaseService;
		_logger = logger;
	}

	/// <summary>
	/// Testa a conexão com o banco de dados
	/// selecionado pelo cabeçalho X-Sigera-Sistema.
	/// </summary>
	[HttpGet("conexao")]
	public async Task<IActionResult> TestarConexao(
		CancellationToken cancellationToken)
	{
		try
		{
			await using var dbContext =
				_databaseService.CreateDbContext();

			var connection = dbContext.Database.GetDbConnection();

			await dbContext.Database.OpenConnectionAsync(
				cancellationToken);

			try
			{
				return Ok(new
				{
					status = "conectado",
					banco = connection.Database,
					servidor = connection.DataSource
				});
			}
			finally
			{
				await dbContext.Database.CloseConnectionAsync();
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Falha ao conectar ao banco do sistema {SystemCode}.",
				Request.Headers["X-Sigera-Sistema"].ToString());

			return this.Erro(
				StatusCodes.Status503ServiceUnavailable,
				"Não foi possível conectar ao banco selecionado. Consulte o log da API.",
				"falha_na_conexao");
		}
	}

	/// <summary>
	/// Consulta as primeiras 10 pessoas cadastradas
	/// na tabela T000PESS do banco selecionado.
	/// Retorna somente campos não sensíveis.
	/// </summary>
	[HttpGet("pessoas")]
	public async Task<IActionResult> ConsultarPessoas(
		CancellationToken cancellationToken)
	{
		await using var dbContext =
			_databaseService.CreateDbContext();

		var pessoas = await dbContext.Pessoas
			.AsNoTracking()
			.OrderBy(p => p.CD_EMP)
			.ThenBy(p => p.SQ_PES)
			.Take(10)
			.Select(p => new
			{
				p.CD_EMP,
				p.SQ_PES,
				p.TP_PES,
				p.NM_PES,
				p.DT_CAD
			})
			.ToListAsync(cancellationToken);

		return Ok(new
		{
			status = "sucesso",
			quantidade = pessoas.Count,
			pessoas
		});
	}
}