using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Cadastros;
using SigeraAssociados.Application.Usuarios;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Tabelas de apoio do cadastro de associados: filiais (T000PARA),
/// situações (T000SCLI), tratamentos (T000TRTA) e municípios (T000MUNI).
/// O sistema vem do cabeçalho X-Sigera-Sistema.
/// Consultar: qualquer usuário com acesso ao sistema.
/// Incluir, alterar e excluir: somente administradores.
/// Exclusão física, recusada (409) quando o registro está em uso.
/// </summary>
[ApiController]
[Route("api/cadastros")]
[Authorize]
public sealed class TabelasApoioController : ControllerBase
{
	private readonly ICadastrosSistemaService _cadastros;
	private readonly IManutencaoTabelasApoioService _manutencao;
	private readonly SigeraSystemContext _sistema;
	private readonly ILogger<TabelasApoioController> _logger;

	public TabelasApoioController(
		ICadastrosSistemaService cadastros,
		IManutencaoTabelasApoioService manutencao,
		SigeraSystemContext sistema,
		ILogger<TabelasApoioController> logger)
	{
		_cadastros = cadastros;
		_manutencao = manutencao;
		_sistema = sistema;
		_logger = logger;
	}

	// O middleware de sistema já validou o cabeçalho e o acesso.
	private int Sistema => _sistema.SystemCode
		?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

	/// <summary>
	/// Filiais do sistema (T000PARA), para o campo filial do associado.
	/// </summary>
	/// <response code="200">Filiais ordenadas pelo código.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("filiais")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> Filiais(CancellationToken cancellationToken)
	{
		var filiais = await _cadastros.ListarFiliaisAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = filiais.Count, filiais });
	}

	/// <summary>
	/// Situações do associado (T000SCLI), para o campo cdSitCli.
	/// </summary>
	/// <response code="200">Situações ordenadas pelo código.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("situacoes")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> Situacoes(CancellationToken cancellationToken)
	{
		var situacoes = await _cadastros.ListarSituacoesAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = situacoes.Count, situacoes });
	}

	/// <summary>
	/// Tratamentos (T000TRTA), para o campo cdTratamento da pessoa física.
	/// </summary>
	/// <response code="200">Tratamentos ordenados pelo código.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("tratamentos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> Tratamentos(CancellationToken cancellationToken)
	{
		var tratamentos = await _cadastros.ListarTratamentosAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = tratamentos.Count, tratamentos });
	}

	/// <summary>
	/// Bancos (T000BANC), para o campo cdBanco da conta do associado.
	/// </summary>
	/// <param name="busca">Começo do nome ou código (opcional).</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Bancos ordenados pelo código.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("bancos")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> Bancos([FromQuery] string? busca, CancellationToken cancellationToken)
	{
		var bancos = await _cadastros.ListarBancosAsync(Sistema, busca, cancellationToken);

		return Ok(new { quantidade = bancos.Count, bancos });
	}

	/// <summary>
	/// Tipos de endereço (T000TEND), para o campo cdTipo do endereço do associado.
	/// </summary>
	/// <response code="200">Tipos ordenados pelo código.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("tipos-endereco")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> TiposEndereco(CancellationToken cancellationToken)
	{
		var tipos = await _cadastros.ListarTiposEnderecoAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = tipos.Count, tiposEndereco = tipos });
	}

	/// <summary>
	/// Tipos de telefone (T000TTEL), para o campo cdTipo do telefone do associado.
	/// </summary>
	/// <response code="200">Tipos ordenados pelo código.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("tipos-telefone")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> TiposTelefone(CancellationToken cancellationToken)
	{
		var tipos = await _cadastros.ListarTiposTelefoneAsync(Sistema, cancellationToken);

		return Ok(new { quantidade = tipos.Count, tiposTelefone = tipos });
	}

	/// <summary>
	/// Pesquisa municípios (T000MUNI), para o campo cdMunicipio da pessoa física.
	/// </summary>
	/// <remarks>
	/// `busca` procura pelo começo do nome ou pelo código do município.
	///
	///     GET /api/cadastros/municipios?uf=CE&amp;busca=JUAZEIRO
	/// </remarks>
	/// <param name="uf">UF com 2 letras (opcional).</param>
	/// <param name="busca">Começo do nome ou código (opcional).</param>
	/// <param name="pagina">Página, a partir de 1.</param>
	/// <param name="tamanhoPagina">Itens por página, de 1 a 200.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Página de municípios ordenada pelo nome.</response>
	/// <response code="400">UF ou paginação inválidas.</response>
	/// <response code="403">Sem acesso ao sistema informado.</response>
	[HttpGet("municipios")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> Municipios(
		[FromQuery] string? uf,
		[FromQuery] string? busca,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 50,
		CancellationToken cancellationToken = default)
	{
		var resultado = await _cadastros.ListarMunicipiosAsync(
			Sistema,
			new ConsultaMunicipiosRequest
			{
				Uf = uf,
				Busca = busca,
				Pagina = pagina,
				TamanhoPagina = tamanhoPagina
			},
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
			quantidade = resultado.Municipios.Count,
			municipios = resultado.Municipios
		});
	}

	// ===================== Filiais =====================

	/// <summary>
	/// Dados completos de uma filial (endereço, telefone, CNPJ).
	/// </summary>
	/// <response code="200">Filial.</response>
	/// <response code="404">Filial não encontrada.</response>
	[HttpGet("filiais/{cdFil:decimal}")]
	[ProducesResponseType(typeof(FilialDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ObterFilial(decimal cdFil, CancellationToken cancellationToken)
	{
		var filial = await _manutencao.ObterFilialAsync(Sistema, cdFil, cancellationToken);

		return filial is null
			? this.Erro(StatusCodes.Status404NotFound, "Filial não encontrada.")
			: Ok(new { filial });
	}

	/// <summary>
	/// Inclui uma filial (T000PARA). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdFil` omitido: a API usa MAX + 1. Obrigatório: `nome`.
	///
	///     POST /api/cadastros/filiais
	///     {
	///       "nome": "FILIAL CRATO",
	///       "nomeCurto": "CRATO",
	///       "logradouro": "RUA DO COMÉRCIO", "numero": 100, "bairro": "CENTRO",
	///       "cep": "63100-000", "municipio": "CRATO", "uf": "CE",
	///       "ddd": 88, "telefone": "35210000",
	///       "cnpj": "12.345.678/0001-95"
	///     }
	/// </remarks>
	/// <response code="201">Filial cadastrada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("filiais")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(FilialDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirFilial([FromBody] FilialDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.IncluirFilialAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Filial incluída", resultado.Item?.CdFil, criado: true);
	}

	/// <summary>
	/// Altera uma filial. Somente administradores.
	/// </summary>
	/// <remarks>Envie todos os campos; o código vem da rota.</remarks>
	/// <response code="200">Filial alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Filial não encontrada.</response>
	[HttpPut("filiais/{cdFil:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(FilialDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarFilial(decimal cdFil, [FromBody] FilialDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarFilialAsync(Sistema, cdFil, dados, cancellationToken);

		return Responder(resultado, "Filial alterada", cdFil);
	}

	/// <summary>
	/// Exclui uma filial. Somente administradores.
	/// </summary>
	/// <remarks>
	/// Recusada (409) se houver associados, contratos, corretores ou
	/// acessos de usuários da API ligados à filial.
	/// </remarks>
	/// <response code="200">Filial excluída.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Filial não encontrada.</response>
	/// <response code="409">Filial em uso.</response>
	[HttpDelete("filiais/{cdFil:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirFilial(decimal cdFil, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirFilialAsync(Sistema, cdFil, cancellationToken);

		return Responder(resultado, "Filial excluída", cdFil);
	}

	// ===================== Situações =====================

	/// <summary>
	/// Inclui uma situação do associado (T000SCLI). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `codigo` omitido: MAX + 1. `descricao` até 30 caracteres.
	///
	///     POST /api/cadastros/situacoes
	///     { "descricao": "SUSPENSO" }
	/// </remarks>
	/// <response code="201">Situação cadastrada.</response>
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
		var resultado = await _manutencao.IncluirSituacaoAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Situação incluída", resultado.Item?.Codigo, criado: true);
	}

	/// <summary>
	/// Altera a descrição de uma situação. Somente administradores.
	/// </summary>
	/// <response code="200">Situação alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Situação não encontrada.</response>
	[HttpPut("situacoes/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarSituacao(decimal codigo, [FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarSituacaoAsync(Sistema, codigo, dados, cancellationToken);

		return Responder(resultado, "Situação alterada", codigo);
	}

	/// <summary>
	/// Exclui uma situação. Somente administradores.
	/// </summary>
	/// <remarks>
	/// Recusada (409) se houver associados nela. A situação 10 (falecido)
	/// nunca pode ser excluída: a trigger TR_CLIE_UPD depende dela.
	/// </remarks>
	/// <response code="200">Situação excluída.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Situação não encontrada.</response>
	/// <response code="409">Situação em uso ou protegida.</response>
	[HttpDelete("situacoes/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirSituacao(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirSituacaoAsync(Sistema, codigo, cancellationToken);

		return Responder(resultado, "Situação excluída", codigo);
	}

	// ===================== Tratamentos =====================

	/// <summary>
	/// Inclui um tratamento (T000TRTA). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `codigo` omitido: MAX + 1. `descricao` até 20 caracteres.
	///
	///     POST /api/cadastros/tratamentos
	///     { "descricao": "DR." }
	/// </remarks>
	/// <response code="201">Tratamento cadastrado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("tratamentos")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirTratamento([FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.IncluirTratamentoAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Tratamento incluído", resultado.Item?.Codigo, criado: true);
	}

	/// <summary>
	/// Altera a descrição de um tratamento. Somente administradores.
	/// </summary>
	/// <response code="200">Tratamento alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Tratamento não encontrado.</response>
	[HttpPut("tratamentos/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarTratamento(decimal codigo, [FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarTratamentoAsync(Sistema, codigo, dados, cancellationToken);

		return Responder(resultado, "Tratamento alterado", codigo);
	}

	/// <summary>
	/// Exclui um tratamento. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver pessoas com o tratamento.</remarks>
	/// <response code="200">Tratamento excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Tratamento não encontrado.</response>
	/// <response code="409">Tratamento em uso.</response>
	[HttpDelete("tratamentos/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirTratamento(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirTratamentoAsync(Sistema, codigo, cancellationToken);

		return Responder(resultado, "Tratamento excluído", codigo);
	}

	// ===================== Bancos =====================

	/// <summary>
	/// Inclui um banco (T000BANC). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdBanco` é obrigatório (código do banco, não é gerado). `nome` até 35.
	///
	///     POST /api/cadastros/bancos
	///     { "cdBanco": 104, "nome": "CAIXA ECONOMICA FEDERAL", "cdSituacao": 1 }
	///
	/// `cdSituacao`: 1 = ATIVO, 0 = INATIVO (omitida: 1).
	/// </remarks>
	/// <response code="201">Banco cadastrado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("bancos")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(BancoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirBanco([FromBody] BancoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.IncluirBancoAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Banco incluído", resultado.Item?.CdBanco, criado: true);
	}

	/// <summary>
	/// Altera nome e situação de um banco. Somente administradores.
	/// </summary>
	/// <response code="200">Banco alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Banco não encontrado.</response>
	[HttpPut("bancos/{cdBanco:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(BancoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarBanco(decimal cdBanco, [FromBody] BancoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarBancoAsync(Sistema, cdBanco, dados, cancellationToken);

		return Responder(resultado, "Banco alterado", cdBanco);
	}

	/// <summary>
	/// Exclui um banco. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver contas no banco.</remarks>
	/// <response code="200">Banco excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Banco não encontrado.</response>
	/// <response code="409">Banco em uso.</response>
	[HttpDelete("bancos/{cdBanco:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirBanco(decimal cdBanco, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirBancoAsync(Sistema, cdBanco, cancellationToken);

		return Responder(resultado, "Banco excluído", cdBanco);
	}

	// ===================== Tipos de endereço =====================

	/// <summary>
	/// Inclui um tipo de endereço (T000TEND). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `codigo` omitido: MAX + 1. `descricao` até 20 caracteres.
	///
	///     POST /api/cadastros/tipos-endereco
	///     { "descricao": "RESIDENCIAL" }
	/// </remarks>
	/// <response code="201">Tipo cadastrado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("tipos-endereco")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirTipoEndereco([FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.IncluirTipoEnderecoAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Tipo de endereço incluído", resultado.Item?.Codigo, criado: true);
	}

	/// <summary>
	/// Altera a descrição de um tipo de endereço. Somente administradores.
	/// </summary>
	/// <response code="200">Tipo alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Tipo não encontrado.</response>
	[HttpPut("tipos-endereco/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarTipoEndereco(decimal codigo, [FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarTipoEnderecoAsync(Sistema, codigo, dados, cancellationToken);

		return Responder(resultado, "Tipo de endereço alterado", codigo);
	}

	/// <summary>
	/// Exclui um tipo de endereço. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver endereços com o tipo.</remarks>
	/// <response code="200">Tipo excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Tipo não encontrado.</response>
	/// <response code="409">Tipo em uso.</response>
	[HttpDelete("tipos-endereco/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirTipoEndereco(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirTipoEnderecoAsync(Sistema, codigo, cancellationToken);

		return Responder(resultado, "Tipo de endereço excluído", codigo);
	}

	// ===================== Tipos de telefone =====================

	/// <summary>
	/// Inclui um tipo de telefone (T000TTEL). Somente administradores.
	/// </summary>
	/// <remarks>
	/// `codigo` omitido: MAX + 1. `descricao` até 20 caracteres.
	///
	///     POST /api/cadastros/tipos-telefone
	///     { "descricao": "CELULAR" }
	/// </remarks>
	/// <response code="201">Tipo cadastrado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("tipos-telefone")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirTipoTelefone([FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.IncluirTipoTelefoneAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Tipo de telefone incluído", resultado.Item?.Codigo, criado: true);
	}

	/// <summary>
	/// Altera a descrição de um tipo de telefone. Somente administradores.
	/// </summary>
	/// <response code="200">Tipo alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Tipo não encontrado.</response>
	[HttpPut("tipos-telefone/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ItemTabelaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarTipoTelefone(decimal codigo, [FromBody] ItemTabelaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarTipoTelefoneAsync(Sistema, codigo, dados, cancellationToken);

		return Responder(resultado, "Tipo de telefone alterado", codigo);
	}

	/// <summary>
	/// Exclui um tipo de telefone. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver telefones com o tipo.</remarks>
	/// <response code="200">Tipo excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Tipo não encontrado.</response>
	/// <response code="409">Tipo em uso.</response>
	[HttpDelete("tipos-telefone/{codigo:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirTipoTelefone(decimal codigo, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirTipoTelefoneAsync(Sistema, codigo, cancellationToken);

		return Responder(resultado, "Tipo de telefone excluído", codigo);
	}

	// ===================== Municípios =====================

	/// <summary>
	/// Inclui um município (T000MUNI). Somente administradores.
	/// </summary>
	/// <remarks>
	/// Obrigatórios: `cdMun` (ex.: código do IBGE), `nome` e `uf`.
	///
	///     POST /api/cadastros/municipios
	///     { "cdMun": 2307304, "nome": "JUAZEIRO DO NORTE", "uf": "CE" }
	/// </remarks>
	/// <response code="201">Município cadastrado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("municipios")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(MunicipioDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirMunicipio([FromBody] MunicipioDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.IncluirMunicipioAsync(Sistema, dados, cancellationToken);

		return Responder(resultado, "Município incluído", resultado.Item?.CdMun, criado: true);
	}

	/// <summary>
	/// Altera nome e UF de um município. Somente administradores.
	/// </summary>
	/// <response code="200">Município alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Município não encontrado.</response>
	[HttpPut("municipios/{cdMun:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(MunicipioDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarMunicipio(decimal cdMun, [FromBody] MunicipioDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.AlterarMunicipioAsync(Sistema, cdMun, dados, cancellationToken);

		return Responder(resultado, "Município alterado", cdMun);
	}

	/// <summary>
	/// Exclui um município. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver pessoas ou corretores ligados ao município.</remarks>
	/// <response code="200">Município excluído.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Município não encontrado.</response>
	/// <response code="409">Município em uso.</response>
	[HttpDelete("municipios/{cdMun:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirMunicipio(decimal cdMun, CancellationToken cancellationToken)
	{
		var resultado = await _manutencao.ExcluirMunicipioAsync(Sistema, cdMun, cancellationToken);

		return Responder(resultado, "Município excluído", cdMun);
	}

	/// <summary>
	/// Converte o resultado em resposta HTTP e registra a operação.
	/// </summary>
	private IActionResult Responder<T>(
		ResultadoTabelaApoio<T> resultado,
		string operacao,
		decimal? codigo,
		bool criado = false)
	{
		if (resultado.Status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: código {Codigo} no sistema {Sistema} por {Usuario}.",
				operacao,
				codigo,
				Sistema,
				User.Identity?.Name ?? "(desconhecido)");

			var corpo = new { mensagem = resultado.Mensagem, item = resultado.Item };

			return criado ? StatusCode(StatusCodes.Status201Created, corpo) : Ok(corpo);
		}

		var status = resultado.Status switch
		{
			StatusTabelaApoio.NaoEncontrado => StatusCodes.Status404NotFound,
			StatusTabelaApoio.Invalido => StatusCodes.Status400BadRequest,
			StatusTabelaApoio.Conflito => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status500InternalServerError
		};

		return this.Erro(status, resultado.Mensagem ?? RespostasErro.Titulo(status));
	}
}
