using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Interfaces.Associados;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Consulta e cadastro (inclusão, alteração e exclusão lógica) de associados
/// pessoa física do sistema escolhido no cabeçalho X-Sigera-Sistema.
/// A empresa é a da base do sistema (cada sistema tem uma só).
/// Tudo respeita os acessos do usuário: quem tem acesso por filial só
/// grava na filial; o corretor só vê e grava os associados com contrato
/// dele ou cadastrados por ele. Excluir: Administradores e Gestores.
/// A validação dos parâmetros fica no AssociadosService (erros viram 400);
/// falhas inesperadas são tratadas pelo TratadorExcecoes (500, com log).
/// </summary>
[Authorize]
[ApiController]
[Route("api/associados")]
public sealed class AssociadosController : ControllerBase
{
	private readonly IAssociadosService _associadosService;
	private readonly ICadastroAssociadosService _cadastro;
	private readonly ILogger<AssociadosController> _logger;

	public AssociadosController(
		IAssociadosService associadosService,
		ICadastroAssociadosService cadastro,
		ILogger<AssociadosController> logger)
	{
		_associadosService = associadosService;
		_cadastro = cadastro;
		_logger = logger;
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
	/// Cadastro completo de um associado pessoa física (para a tela de edição).
	/// </summary>
	/// <remarks>
	/// O CPF vem completo. Associado excluído, pessoa jurídica ou fora dos
	/// seus acessos: 404.
	/// </remarks>
	/// <param name="sqPesCli">Código da pessoa do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Cadastro do associado.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpGet("{sqPesCli:decimal}")]
	[ProducesResponseType(typeof(AssociadoCadastroDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Obter(
		decimal sqPesCli,
		CancellationToken cancellationToken)
	{
		var associado = await _cadastro.ObterAsync(sqPesCli, cancellationToken);

		return associado is null
			? this.Erro(StatusCodes.Status404NotFound, "Associado não encontrado.")
			: Ok(new { status = "sucesso", associado });
	}

	/// <summary>
	/// Inclui um associado pessoa física (T000PESS + T000PFIS + T000CLIE).
	/// </summary>
	/// <remarks>
	/// A API preenche: empresa (da base), TP_PES = F, SQ_PES = MAX + 1,
	/// data e usuário do cadastro. Obrigatórios: nome, CPF, data de
	/// nascimento, sexo, filial e situação. As listas da tela vêm de
	/// GET /api/cadastros/filiais, /situacoes, /tratamentos e /municipios.
	///
	/// Quem tem acesso só pela carteira de corretor: o associado fica
	/// vinculado a esse corretor (sqPesCorretor), e ele passa a ver e alterar
	/// o associado mesmo antes de haver contrato.
	///
	/// Situação 10 (falecido) só por Administradores e Gestores.
	///
	///     POST /api/associados
	///     {
	///       "nome": "MARIA DA SILVA",
	///       "email": "maria@exemplo.com",
	///       "pessoaFisica": {
	///         "cpf": "123.456.789-09",
	///         "dataNascimento": "1980-05-10",
	///         "sexo": "F",
	///         "cdMunicipio": 2307304,
	///         "nomeMae": "ANA DA SILVA"
	///       },
	///       "associado": { "cdFil": 1, "cdSitCli": 1 }
	///     }
	/// </remarks>
	/// <response code="201">Cadastrado; devolve o cadastro com o sqPesCli gerado.</response>
	/// <response code="400">Dados inválidos, ou filial, situação, município, tratamento ou corretor inexistente.</response>
	/// <response code="403">Filial fora dos seus acessos, ou situação falecido sem ser Administrador ou Gestor.</response>
	/// <response code="409">CPF já cadastrado em outra pessoa.</response>
	[HttpPost]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(AssociadoCadastroDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir(
		[FromBody] CadastroAssociadoDados dados,
		CancellationToken cancellationToken)
	{
		var resultado = await _cadastro.IncluirAsync(dados, Executor(), cancellationToken);

		if (resultado.Status != StatusCadastroAssociado.Sucesso)
		{
			return ParaErro(resultado);
		}

		Registrar("Associado incluído", resultado.Associado!.SqPesCli);

		return CreatedAtAction(
			nameof(Obter),
			new { sqPesCli = resultado.Associado.SqPesCli },
			new { mensagem = resultado.Mensagem, associado = resultado.Associado });
	}

	/// <summary>
	/// Altera um associado pessoa física (substitui os dados).
	/// </summary>
	/// <remarks>
	/// Envie o cadastro inteiro, no mesmo formato da inclusão. Só as colunas
	/// que mudaram são gravadas.
	///
	/// Atenção: mudar a situação para 10 (falecido) aciona a trigger
	/// TR_CLIE_UPD, que encerra os contratos ativos (situação 2 ou 5 vira 7).
	/// Só Administradores e Gestores podem fazer isso; a resposta informa
	/// quantos contratos foram encerrados (contratosEncerrados).
	///
	/// Mudar de filial exige acesso às duas filiais.
	/// </remarks>
	/// <param name="sqPesCli">Código da pessoa do associado.</param>
	/// <param name="dados">Cadastro completo.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Alterado; devolve o cadastro atualizado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Nova filial fora dos seus acessos, ou falecido sem ser Administrador ou Gestor.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">CPF já cadastrado em outra pessoa.</response>
	[HttpPut("{sqPesCli:decimal}")]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(AssociadoCadastroDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(
		decimal sqPesCli,
		[FromBody] CadastroAssociadoDados dados,
		CancellationToken cancellationToken)
	{
		var resultado = await _cadastro.AlterarAsync(sqPesCli, dados, Executor(), cancellationToken);

		if (resultado.Status != StatusCadastroAssociado.Sucesso)
		{
			return ParaErro(resultado);
		}

		Registrar(
			resultado.ContratosEncerrados is null ? "Associado alterado" : "Associado marcado como falecido",
			sqPesCli);

		return Ok(new
		{
			mensagem = resultado.Mensagem,
			contratosEncerrados = resultado.ContratosEncerrados,
			associado = resultado.Associado
		});
	}

	/// <summary>
	/// Exclui o associado (exclusão lógica).
	/// </summary>
	/// <remarks>
	/// Grava data e usuário em T000CLIE (DT_EXC_API e NM_USU_EXC_API); nada é
	/// apagado e a situação não muda. O associado deixa de aparecer na API;
	/// o sistema legado continua mostrando.
	///
	/// Somente Administradores e Gestores.
	/// </remarks>
	/// <param name="sqPesCli">Código da pessoa do associado.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpDelete("{sqPesCli:decimal}")]
	[Authorize(Roles = Grupos.PerfisExclusaoAssociados)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(
		decimal sqPesCli,
		CancellationToken cancellationToken)
	{
		var resultado = await _cadastro.ExcluirAsync(sqPesCli, Executor(), cancellationToken);

		if (resultado.Status != StatusCadastroAssociado.Sucesso)
		{
			return ParaErro(resultado);
		}

		Registrar("Associado excluído", sqPesCli);

		return Ok(new { mensagem = resultado.Mensagem, sqPesCli });
	}

	/// <summary>
	/// Consulta associados com filtros, paginação e ordenação.
	/// cdFil: filial (T000CLIE.CD_FIL); soma com os seus acessos.
	/// nome: começo do nome (ex.: "MARIA" encontra "MARIA DA SILVA").
	/// cpf: CPF completo, com ou sem pontuação (busca exata; só pessoa física).
	/// ordenarPor: nome, cdfil, sqpescli, cdsitcli, situacao ou dtcarteira.
	/// direcao: asc ou desc. tamanhoPagina: 1 a 100.
	/// </summary>
	[HttpGet("consulta")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> ConsultarPaginado(
		[FromQuery] decimal? cdFil,
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
				CdFil = cdFil,
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
				cdFil,
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

	/// <summary>
	/// Login (e-mail) de quem grava e se pode marcar falecido.
	/// </summary>
	private ExecutorCadastro Executor()
	{
		var login = User.FindFirstValue(ClaimTypes.Email)
			?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
			?? User.Identity?.Name
			?? "api";

		return new ExecutorCadastro(
			login,
			Grupos.PodemMarcarFalecido.Any(User.IsInRole));
	}

	private void Registrar(string operacao, decimal sqPesCli)
	{
		_logger.LogInformation(
			"{Operacao}: associado {SqPesCli} por {Usuario}.",
			operacao,
			sqPesCli,
			Executor().Login);
	}

	private IActionResult ParaErro(ResultadoCadastroAssociado resultado)
	{
		var status = resultado.Status switch
		{
			StatusCadastroAssociado.NaoEncontrado => StatusCodes.Status404NotFound,
			StatusCadastroAssociado.Invalido => StatusCodes.Status400BadRequest,
			StatusCadastroAssociado.Proibido => StatusCodes.Status403Forbidden,
			StatusCadastroAssociado.Conflito => StatusCodes.Status409Conflict,
			_ => StatusCodes.Status500InternalServerError
		};

		return this.Erro(status, resultado.Mensagem ?? RespostasErro.Titulo(status));
	}
}
