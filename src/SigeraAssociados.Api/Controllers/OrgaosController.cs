using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Orgaos;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Application.Usuarios;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Órgãos (T000ORGA) onde os associados servidores públicos trabalham,
/// e as tabelas de apoio dos vínculos: lotações (T000LOTA) e situações
/// do vínculo (T000SPOG) e cargos (T000CARG); e as do órgão: grupos (T009GRUP)
/// e subórgãos (T009SBOR). Sistema pelo cabeçalho X-Sigera-Sistema.
/// Consultar: qualquer usuário com acesso ao sistema.
/// Incluir, alterar e excluir: somente administradores.
/// Os vínculos de cada associado ficam em /api/associados/{sqPesCli}/orgaos.
/// </summary>
[ApiController]
[Route("api/orgaos")]
[Authorize]
public sealed class OrgaosController : ControllerBase
{
	private readonly IOrgaosService _orgaos;
	private readonly SigeraSystemContext _sistema;
	private readonly ILogger<OrgaosController> _logger;

	public OrgaosController(
		IOrgaosService orgaos,
		SigeraSystemContext sistema,
		ILogger<OrgaosController> logger)
	{
		_orgaos = orgaos;
		_sistema = sistema;
		_logger = logger;
	}

	// O middleware de sistema já validou o cabeçalho e o acesso.
	private int Sistema => _sistema.SystemCode
		?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

	// ===================== Órgãos =====================

	/// <summary>
	/// Pesquisa órgãos.
	/// </summary>
	/// <remarks>
	/// `busca`: começo do nome, começo da sigla ou código.
	///
	///     GET /api/orgaos?busca=SECRETARIA&amp;uf=CE
	/// </remarks>
	/// <param name="busca">Começo do nome ou da sigla, ou o código (opcional).</param>
	/// <param name="cdFil">Filial (opcional).</param>
	/// <param name="uf">UF (opcional).</param>
	/// <param name="cdSituacao">Situação do órgão: 1 = ATIVO, 0 = INATIVO (opcional).</param>
	/// <param name="pagina">Página, a partir de 1.</param>
	/// <param name="tamanhoPagina">Itens por página, de 1 a 200.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Página de órgãos ordenada pelo nome.</response>
	/// <response code="400">Paginação inválida.</response>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Listar(
		[FromQuery] string? busca,
		[FromQuery] decimal? cdFil,
		[FromQuery] string? uf,
		[FromQuery] decimal? cdSituacao,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 50,
		CancellationToken cancellationToken = default)
	{
		var resultado = await _orgaos.ListarAsync(
			Sistema,
			new ConsultaOrgaosRequest { Busca = busca, CdFil = cdFil, Uf = uf, CdSituacao = cdSituacao, Pagina = pagina, TamanhoPagina = tamanhoPagina },
			cancellationToken);

		return Ok(new
		{
			paginacao = new
			{
				pagina = resultado.Pagina,
				tamanhoPagina = resultado.TamanhoPagina,
				totalRegistros = resultado.TotalRegistros,
				totalPaginas = resultado.TotalPaginas
			},
			quantidade = resultado.Orgaos.Count,
			orgaos = resultado.Orgaos
		});
	}

	/// <summary>
	/// Dados completos de um órgão.
	/// </summary>
	/// <response code="200">Órgão.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpGet("{cdOrg:decimal}")]
	[ProducesResponseType(typeof(OrgaoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Obter(decimal cdOrg, CancellationToken cancellationToken)
	{
		var orgao = await _orgaos.ObterAsync(Sistema, cdOrg, cancellationToken);

		return orgao is null
			? this.Erro(StatusCodes.Status404NotFound, "Órgão não encontrado.")
			: Ok(new { orgao });
	}

	/// <summary>
	/// Inclui um órgão (T000ORGA). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdOrg` omitido: MAX + 1. Obrigatório: `nome`. Filial, grupo
	/// (GET /api/orgaos/grupos), subórgão (GET /api/orgaos/suborgaos) e
	/// municípios são conferidos nas tabelas.
	///
	///     POST /api/orgaos
	///     {
	///       "nome": "SECRETARIA DA EDUCACAO",
	///       "sigla": "SEDUC",
	///       "uf": "CE",
	///       "cdFil": 1,
	///       "endereco": { "logradouro": "AV. GENERAL AFONSO ALBUQUERQUE LIMA", "numero": 1, "bairro": "CAMBEBA", "cep": "60822-325", "cdMunicipio": 2304400 },
	///       "contatoAdministrativo": { "nome": "MARIA", "telefone": "85 3101-0000", "email": "adm@seduc.ce.gov.br" },
	///       "contatoFinanceiro": { "nome": "JOSE", "telefone": "85 3101-0001", "email": "fin@seduc.ce.gov.br" },
	///       "empresaProcessadora": { "nome": "ETICE", "contato": "SUPORTE", "telefone": "85 3101-0002" },
	///       "dataPagamento": "2026-11-05"
	///     }
	/// </remarks>
	/// <response code="201">Órgão cadastrado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(OrgaoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir([FromBody] OrgaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.IncluirAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Órgão incluído");
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um órgão (envie todos os campos). Somente administradores.
	/// </summary>
	/// <response code="200">Órgão alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("{cdOrg:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(OrgaoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Alterar(decimal cdOrg, [FromBody] OrgaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.AlterarAsync(Sistema, cdOrg, dados, cancellationToken);
		Registrar(resultado.Status, "Órgão alterado");
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um órgão. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver associados vinculados ou outra tabela usando o órgão.</remarks>
	/// <response code="200">Órgão excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Órgão em uso.</response>
	[HttpDelete("{cdOrg:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Excluir(decimal cdOrg, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.ExcluirAsync(Sistema, cdOrg, cancellationToken);
		Registrar(resultado.Status, "Órgão excluído");
		return this.Converter(resultado);
	}

	// ===================== Grupos =====================

	/// <summary>
	/// Grupos de órgãos (T009GRUP), para o campo cdGrupo do órgão.
	/// </summary>
	[HttpGet("grupos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> ListarGrupos(CancellationToken cancellationToken)
	{
		var itens = await _orgaos.ListarGruposAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = itens.Count, grupos = itens });
	}

	/// <summary>
	/// Inclui um grupo de órgãos (T009GRUP). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdGrupo` omitido: MAX + 1. Obrigatórios:
	/// - `descricao` (até 30);
	/// - `tipo` (TP_GP): F = DIA FIXO, U = DIA ÚTIL;
	/// - `ordem` (ID_ORD): A = ASCENDENTE, D = DESCENDENTE;
	/// - `numeroDias` (NR_DD): 1 a 31.
	///
	/// A resposta traz também `descricaoTipo` e `descricaoOrdem`.
	///
	///     POST /api/orgaos/grupos
	///     { "descricao": "ESTADUAL", "tipo": "F", "ordem": "A", "numeroDias": 5 }
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("grupos")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(GrupoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirGrupo([FromBody] GrupoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.IncluirGrupoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Grupo incluído");
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um grupo de órgãos (T009GRUP). Somente administradores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("grupos/{cdGrupo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(GrupoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarGrupo(decimal cdGrupo, [FromBody] GrupoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.AlterarGrupoAsync(Sistema, cdGrupo, dados, cancellationToken);
		Registrar(resultado.Status, "Grupo alterado");
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um grupo de órgãos (T009GRUP). Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver órgãos no grupo ou outra tabela do sistema usando-o.</remarks>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Em uso.</response>
	[HttpDelete("grupos/{cdGrupo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirGrupo(decimal cdGrupo, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.ExcluirGrupoAsync(Sistema, cdGrupo, cancellationToken);
		Registrar(resultado.Status, "Grupo excluído");
		return this.Converter(resultado);
	}

	// ===================== Subórgãos =====================

	/// <summary>
	/// Subórgãos (T009SBOR), para o campo sqSubOrgao do órgão.
	/// </summary>
	[HttpGet("suborgaos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> ListarSubOrgaos(CancellationToken cancellationToken)
	{
		var itens = await _orgaos.ListarSubOrgaosAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = itens.Count, suborgaos = itens });
	}

	/// <summary>
	/// Inclui um subórgão (T009SBOR). Somente administradores.
	/// </summary>
	/// <remarks>
	/// O código (`sqSubOrgao`) é gerado pelo banco (IDENTITY) e volta na resposta. Obrigatório: `nome` (até 100).
	///
	///     POST /api/orgaos/suborgaos
	///     { "nome": "SECRETARIA REGIONAL CARIRI" }
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("suborgaos")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(SubOrgaoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirSubOrgao([FromBody] SubOrgaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.IncluirSubOrgaoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Subórgão incluído");
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um subórgão (T009SBOR). Somente administradores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("suborgaos/{sqSubOrgao:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(SubOrgaoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarSubOrgao(decimal sqSubOrgao, [FromBody] SubOrgaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.AlterarSubOrgaoAsync(Sistema, sqSubOrgao, dados, cancellationToken);
		Registrar(resultado.Status, "Subórgão alterado");
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um subórgão (T009SBOR). Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver órgãos ligados ao subórgão.</remarks>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Em uso.</response>
	[HttpDelete("suborgaos/{sqSubOrgao:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirSubOrgao(decimal sqSubOrgao, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.ExcluirSubOrgaoAsync(Sistema, sqSubOrgao, cancellationToken);
		Registrar(resultado.Status, "Subórgão excluído");
		return this.Converter(resultado);
	}

	// ===================== Lotações =====================

	/// <summary>
	/// Lotações (T000LOTA), para o campo cdLotacao do vínculo.
	/// </summary>
	/// <param name="busca">Começo da descrição ou código (opcional). Até 200 itens.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	[HttpGet("lotacoes")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Lotacoes([FromQuery] string? busca, CancellationToken cancellationToken)
	{
		var lotacoes = await _orgaos.ListarLotacoesAsync(Sistema, busca, cancellationToken);

		return Ok(new { quantidade = lotacoes.Count, lotacoes });
	}

	/// <summary>
	/// Inclui uma lotação (T000LOTA). Somente administradores.
	/// </summary>
	/// <remarks>
	/// Código omitido: MAX + 1.
	///
	///     POST /api/orgaos/lotacoes
	///     { "descricao": "ESCOLA MUNICIPAL CENTRO", "cdSituacao": 1 }
	///
	/// `cdSituacao`: 1 = ATIVO, 2 = INATIVO (omitida: 1).
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("lotacoes")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(LotacaoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirLotacao([FromBody] LotacaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.IncluirLotacaoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Lotacao incluída(o)");
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma lotação (T000LOTA). Somente administradores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("lotacoes/{cdLot:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(LotacaoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarLotacao(decimal cdLot, [FromBody] LotacaoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.AlterarLotacaoAsync(Sistema, cdLot, dados, cancellationToken);
		Registrar(resultado.Status, "Lotacao alterada(o)");
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma lotação (T000LOTA). Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver vínculos com a lotação.</remarks>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Em uso.</response>
	[HttpDelete("lotacoes/{cdLot:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirLotacao(decimal cdLot, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.ExcluirLotacaoAsync(Sistema, cdLot, cancellationToken);
		Registrar(resultado.Status, "Lotacao excluída(o)");
		return this.Converter(resultado);
	}

	// ===================== Cargos =====================

	/// <summary>
	/// Cargos (T000CARG), para o campo cdCargo do vínculo.
	/// </summary>
	/// <param name="busca">Começo da descrição ou código (opcional). Até 200 itens.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	[HttpGet("cargos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Cargos([FromQuery] string? busca, CancellationToken cancellationToken)
	{
		var cargos = await _orgaos.ListarCargosAsync(Sistema, busca, cancellationToken);

		return Ok(new { quantidade = cargos.Count, cargos });
	}

	/// <summary>
	/// Inclui um cargo (T000CARG). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdCargo` omitido: MAX + 1. `descricao` até 30.
	///
	///     POST /api/orgaos/cargos
	///     { "descricao": "PROFESSOR", "cdSituacao": 1 }
	///
	/// `cdSituacao`: 1 = ATIVO, 0 = INATIVO (omitida: 1).
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("cargos")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CargoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirCargo([FromBody] CargoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.IncluirCargoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Cargo incluído");
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um cargo. Somente administradores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("cargos/{cdCargo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CargoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarCargo(decimal cdCargo, [FromBody] CargoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.AlterarCargoAsync(Sistema, cdCargo, dados, cancellationToken);
		Registrar(resultado.Status, "Cargo alterado");
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um cargo. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver vínculos com o cargo.</remarks>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Em uso.</response>
	[HttpDelete("cargos/{cdCargo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirCargo(decimal cdCargo, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.ExcluirCargoAsync(Sistema, cdCargo, cancellationToken);
		Registrar(resultado.Status, "Cargo excluído");
		return this.Converter(resultado);
	}

	// ===================== Situações do vínculo =====================

	/// <summary>
	/// Situações do vínculo com o órgão (T000SPOG), para o campo cdSituacao do vínculo.
	/// </summary>
	[HttpGet("situacoes")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Situacoes(CancellationToken cancellationToken)
	{
		var situacoes = await _orgaos.ListarSituacoesAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = situacoes.Count, situacoes });
	}

	/// <summary>
	/// Inclui uma situação do vínculo (T000SPOG). Somente administradores.
	/// </summary>
	/// <remarks>
	/// Código omitido: MAX + 1.
	///
	///     POST /api/orgaos/situacoes
	///     { "descricao": "ATIVO" }
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("situacoes")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirSituacao([FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.IncluirSituacaoAsync(Sistema, dados, cancellationToken);
		Registrar(resultado.Status, "Situacao incluída(o)");
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma situação do vínculo (T000SPOG). Somente administradores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpPut("situacoes/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarSituacao(decimal codigo, [FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.AlterarSituacaoAsync(Sistema, codigo, dados, cancellationToken);
		Registrar(resultado.Status, "Situacao alterada(o)");
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma situação do vínculo (T000SPOG). Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver vínculos na situação.</remarks>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">Em uso.</response>
	[HttpDelete("situacoes/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirSituacao(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _orgaos.ExcluirSituacaoAsync(Sistema, codigo, cancellationToken);
		Registrar(resultado.Status, "Situacao excluída(o)");
		return this.Converter(resultado);
	}

	private void Registrar(StatusTabelaApoio status, string operacao)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao} no sistema {Sistema} por {Usuario}.",
				operacao,
				Sistema,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
