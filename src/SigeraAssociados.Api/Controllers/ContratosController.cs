using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;
using SigeraAssociados.Application.Interfaces.Contratos;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Contratos do associado (T009CTRT), com as regras da tela "Cadastrar
/// Contrato" do legado. A chave é filial + número do contrato.
///
/// Permissões iguais às do associado: consultar, incluir e alterar dentro
/// dos acessos (quem tem a carteira de um corretor só vê e grava os
/// contratos desse corretor); excluir só Administradores e Gestores
/// (exclusão lógica). Contrato fora dos acessos: 404.
/// </summary>
[ApiController]
[Route("api/contratos")]
[Authorize]
public sealed class ContratosController : ControllerBase
{
	private readonly IContratosService _contratos;
	private readonly ICategoriasContratoService _categorias;
	private readonly ILogger<ContratosController> _logger;

	public ContratosController(
		IContratosService contratos,
		ICategoriasContratoService categorias,
		ILogger<ContratosController> logger)
	{
		_contratos = contratos;
		_categorias = categorias;
		_logger = logger;
	}

	/// <summary>
	/// Pesquisa contratos (não excluídos, de associados não excluídos).
	/// </summary>
	/// <remarks>
	/// `busca`: contrato na máscara ##-###### (filial + número, ex.: 01-000123),
	/// número do contrato, CPF do associado (11 dígitos, com ou sem pontuação)
	/// ou começo do nome do associado. Ordem: data do contrato
	/// (mais recente primeiro), depois nome do associado e número do contrato.
	///
	///     GET /api/contratos?sqPesCli=12345
	///     GET /api/contratos?busca=MARIA&amp;cdSituacao=2&amp;cdFil=1
	///     GET /api/contratos?busca=01-000123
	/// </remarks>
	/// <response code="200">Página de contratos.</response>
	/// <response code="400">Paginação inválida.</response>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Pesquisar(
		[FromQuery] string? busca,
		[FromQuery] decimal? sqPesCli,
		[FromQuery] decimal? cdFil,
		[FromQuery] decimal? cdSituacao,
		[FromQuery] decimal? sqPesCorretor,
		[FromQuery] decimal? cdProduto,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 50,
		CancellationToken cancellationToken = default)
	{
		var resultado = await _contratos.PesquisarAsync(
			new PesquisaContratosRequest
			{
				Busca = busca,
				SqPesCli = sqPesCli,
				CdFil = cdFil,
				CdSituacao = cdSituacao,
				SqPesCorretor = sqPesCorretor,
				CdProduto = cdProduto,
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
			quantidade = resultado.Contratos.Count,
			contratos = resultado.Contratos
		});
	}

	/// <summary>
	/// Contrato completo, com descrições e parcelas.
	/// </summary>
	/// <response code="200">Contrato.</response>
	/// <response code="404">Não encontrado, excluído ou fora dos acessos.</response>
	[HttpGet("{cdFil:decimal}/{sqCtr:decimal}")]
	[ProducesResponseType(typeof(ContratoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Obter(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken)
	{
		var contrato = await _contratos.ObterAsync(cdFil, sqCtr, cancellationToken);

		return contrato is null
			? this.Erro(StatusCodes.Status404NotFound, "Contrato não encontrado.")
			: Ok(contrato);
	}

	/// <summary>
	/// Inclui um contrato e gera as parcelas.
	/// </summary>
	/// <remarks>
	/// A API numera o contrato (`sqCtr` = MAX + 1 da filial) e gera
	/// `quantidadeParcelas` parcelas, como o botão "gerar parcelas" do legado:
	/// valor = mensalidade + cobertura do pecúlio; a 1ª vence em
	/// `dataPrimeiraParcela` e as demais mês a mês.
	///
	/// Obrigatórios: `cdFil`, `sqPesCli`, `cdProduto`, `cdConvenio`,
	/// `cdFormaPagamento`, `cdSituacao`, `sqOrgao` (vínculo do associado),
	/// `sqContaAssociado`, `dataPrimeiraParcela` (depois de `dataContrato`),
	/// `valorContrato`, `quantidadeParcelas`, `cdCategoria` e `sqPesCorretor`
	/// (este, salvo para quem só tem a carteira de um corretor).
	///
	/// Mensalidade: envie `valorMensalidade` ou `indice` (mensalidade = valor
	/// x índice). Sem os dois, com `cdCarencia`, o índice vem de T009INDI
	/// para a carência e a quantidade de parcelas.
	///
	/// Situação: 9 e 13 exigem `dataQuitacao`; 12, `dataFinalizacao`; 10,
	/// `dataCancelamento` (e essas datas só valem com essas situações). Com 4
	/// ou 9, `dataEncerramento` = hoje.
	///
	/// Listas fixas: `cdTipoContrato` 1 NOVO, 2 RENOVAÇÃO, 3 COMPRA DE DÍVIDA,
	/// 4 COMPLEMENTO DE MARGEM, 5 REFIN E COMPRA DE DÍVIDA; `tipoCompraDivida`
	/// 1 INTERNA, 2 EXTERNA; `cdEntFisi` 1 ENTREGUE, 2 NÃO ENTREGUE, 3 PENDENTE.
	/// Compra de dívida: `valorCompraDivida` maior que zero exige
	/// `tipoCompraDivida`, e o tipo exige o valor (valor zero grava tipo nulo).
	///
	/// Depósito ao associado: `cdOrigemRecurso`, `dataPagamentoAuxilio`,
	/// `valorDeposito` (maior que zero) e `cdBanco` juntos ou nenhum. No
	/// PLANO ASSOCIATIVO (produto 1) esses quatro são ignorados.
	/// Comissão (`valorComissao`, `dataPagamentoComissao`) não é gravada aqui.
	///
	///     POST /api/contratos
	///     {
	///       "cdFil": 1, "sqPesCli": 12345, "cdProduto": 2, "cdConvenio": 1,
	///       "cdFormaPagamento": 1, "cdSituacao": 2, "sqOrgao": 1,
	///       "sqContaAssociado": 1, "dataPrimeiraParcela": "2026-12-10",
	///       "valorContrato": 5000.00, "quantidadeParcelas": 24,
	///       "valorMensalidade": 280.50, "cdCategoria": 1, "sqPesCorretor": 678
	///     }
	/// </remarks>
	/// <response code="201">Contrato incluído, com as parcelas.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Filial ou corretor fora dos seus acessos.</response>
	/// <response code="404">Associado não encontrado ou fora dos acessos.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ContratoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Incluir([FromBody] ContratoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _contratos.IncluirAsync(dados, Login(), cancellationToken);
		Registrar(resultado.Status, "Contrato incluído", resultado.Item?.CdFil, resultado.Item?.SqCtr);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera o contrato (envie o contrato inteiro). Não mexe nas parcelas.
	/// </summary>
	/// <remarks>
	/// `sqPesCli` e `cdProduto` não mudam (400 se vierem diferentes).
	/// `dataContrato` pode mudar (só a data; omitida mantém a gravada), e a 1ª
	/// parcela tem de vencer depois dela. `quantidadeParcelas` é ignorada:
	/// acompanha as parcelas (/api/contratos/{cdFil}/{sqCtr}/parcelas). Mudar
	/// a mensalidade não regera parcelas. Mesmas regras da inclusão (situação,
	/// datas, listas, compra de dívida, depósito). No PLANO ASSOCIATIVO o
	/// depósito gravado é mantido. A comissão gravada é mantida.
	/// </remarks>
	/// <response code="200">Contrato alterado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Corretor fora dos seus acessos.</response>
	/// <response code="404">Contrato não encontrado.</response>
	[HttpPut("{cdFil:decimal}/{sqCtr:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ContratoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Alterar(decimal cdFil, decimal sqCtr, [FromBody] ContratoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _contratos.AlterarAsync(cdFil, sqCtr, dados, cancellationToken);
		Registrar(resultado.Status, "Contrato alterado", cdFil, sqCtr);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui o contrato (exclusão lógica). Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Grava DT_EXC_API e NM_USU_EXC_API em T009CTRT; contrato e parcelas
	/// continuam no banco, mas somem da API. Para encerrar um contrato de
	/// verdade use a situação (cancelado = 10).
	/// </remarks>
	/// <response code="200">Contrato excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Contrato não encontrado.</response>
	[HttpDelete("{cdFil:decimal}/{sqCtr:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(typeof(ContratoResumoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken)
	{
		var resultado = await _contratos.ExcluirAsync(cdFil, sqCtr, Login(), cancellationToken);
		Registrar(resultado.Status, "Contrato excluído", cdFil, sqCtr);
		return this.Converter(resultado);
	}

	// ===================== Tabelas de apoio (consulta) =====================

	/// <summary>Situações do contrato (T009SCTR).</summary>
	[HttpGet("situacoes")]
	[ProducesResponseType(typeof(IReadOnlyList<ApoioContratoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Situacoes(CancellationToken cancellationToken) =>
		Ok(await _contratos.SituacoesAsync(cancellationToken));

	/// <summary>Carências (T009CARE).</summary>
	[HttpGet("carencias")]
	[ProducesResponseType(typeof(IReadOnlyList<ApoioContratoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Carencias(CancellationToken cancellationToken) =>
		Ok(await _contratos.CarenciasAsync(cancellationToken));

	/// <summary>Origens do recurso (T009OREC); `tipo` = TP_ORI_REC.</summary>
	[HttpGet("origens-recurso")]
	[ProducesResponseType(typeof(IReadOnlyList<ApoioContratoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> OrigensRecurso(CancellationToken cancellationToken) =>
		Ok(await _contratos.OrigensRecursoAsync(cancellationToken));

	/// <summary>Produtores (T009PROD), de um corretor ou de todos.</summary>
	[HttpGet("produtores")]
	[ProducesResponseType(typeof(IReadOnlyList<ApoioContratoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Produtores([FromQuery] decimal? sqPesCorretor, CancellationToken cancellationToken) =>
		Ok(await _contratos.ProdutoresAsync(sqPesCorretor, cancellationToken));

	/// <summary>
	/// Índices da mensalidade (T009INDI) por carência e quantidade de parcelas.
	/// </summary>
	[HttpGet("indices")]
	[ProducesResponseType(typeof(IReadOnlyList<IndiceContratoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Indices([FromQuery] decimal? cdCarencia, [FromQuery] decimal? quantidadeParcelas, CancellationToken cancellationToken) =>
		Ok(await _contratos.IndicesAsync(cdCarencia, quantidadeParcelas, cancellationToken));

	// ===================== Categorias (T009CAT) =====================

	/// <summary>
	/// Categorias do contrato (T009CAT), ordenadas pelo nome.
	/// </summary>
	/// <param name="busca">Começo do nome ou código (opcional). Até 500 itens.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Categorias.</response>
	[HttpGet("categorias")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> Categorias([FromQuery] string? busca, CancellationToken cancellationToken)
	{
		var categorias = await _categorias.ListarAsync(busca, cancellationToken);

		return Ok(new { quantidade = categorias.Count, categorias });
	}

	/// <summary>
	/// Inclui uma categoria. Somente administradores.
	/// </summary>
	/// <remarks>
	/// `cdCategoria` omitido: MAX + 1 (até 999). Obrigatório: `descricao` (até 35).
	///
	///     POST /api/contratos/categorias
	///     { "descricao": "SERVIDOR ESTADUAL" }
	/// </remarks>
	/// <response code="201">Incluída.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="409">Código já existe.</response>
	[HttpPost("categorias")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CategoriaContratoDados), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirCategoria([FromBody] CategoriaContratoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _categorias.IncluirAsync(dados, cancellationToken);
		RegistrarCategoria(resultado.Status, "Categoria incluída", resultado.Item?.CdCategoria);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera o nome de uma categoria. Somente administradores.
	/// </summary>
	/// <response code="200">Alterada.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrada.</response>
	[HttpPut("categorias/{cdCategoria:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CategoriaContratoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AlterarCategoria(decimal cdCategoria, [FromBody] CategoriaContratoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _categorias.AlterarAsync(cdCategoria, dados, cancellationToken);
		RegistrarCategoria(resultado.Status, "Categoria alterada", cdCategoria);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma categoria. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver contratos com a categoria.</remarks>
	/// <response code="200">Excluída.</response>
	/// <response code="403">Não é administrador.</response>
	/// <response code="404">Não encontrada.</response>
	/// <response code="409">Em uso por contratos.</response>
	[HttpDelete("categorias/{cdCategoria:decimal}")]
	[Authorize(Roles = Grupos.PerfisAdministracao)]
	[ProducesResponseType(typeof(CategoriaContratoDados), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ExcluirCategoria(decimal cdCategoria, CancellationToken cancellationToken)
	{
		var resultado = await _categorias.ExcluirAsync(cdCategoria, cancellationToken);
		RegistrarCategoria(resultado.Status, "Categoria excluída", cdCategoria);
		return this.Converter(resultado);
	}

	private void RegistrarCategoria(StatusTabelaApoio status, string operacao, decimal? codigo)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation("{Operacao}: categoria {CdCategoria}, por {Usuario}.", operacao, codigo, Login());
		}
	}

	private string Login() =>
		User.FindFirstValue(ClaimTypes.Email)
		?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
		?? User.Identity?.Name
		?? "api";

	private void Registrar(StatusTabelaApoio status, string operacao, decimal? cdFil, decimal? sqCtr)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: filial {CdFil}, contrato {SqCtr}, por {Usuario}.",
				operacao, cdFil, sqCtr, Login());
		}
	}
}
