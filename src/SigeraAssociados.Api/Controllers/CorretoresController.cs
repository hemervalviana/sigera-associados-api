using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Corretores;
using SigeraAssociados.Application.Interfaces.Corretores;
using SigeraAssociados.Application.Interfaces.Orgaos;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Application.Usuarios;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Corretores: pessoa física ou jurídica (nunca as duas) com um registro em
/// T009CRRT. Sistema pelo cabeçalho X-Sigera-Sistema.
/// Consultar: qualquer usuário com acesso ao sistema.
/// Incluir, alterar e excluir: Administradores e Gestores.
/// Exclusão lógica, como a do associado.
/// Tabelas de apoio do corretor: cargos (T000CARG), em /api/corretores/cargos.
/// </summary>
[ApiController]
[Route("api/corretores")]
[Authorize]
public sealed class CorretoresController : ControllerBase
{
	private readonly ICadastroCorretoresService _corretores;
	private readonly IOrgaosService _orgaos;
	private readonly SigeraSystemContext _sistema;
	private readonly ILogger<CorretoresController> _logger;

	public CorretoresController(
		ICadastroCorretoresService corretores,
		IOrgaosService orgaos,
		SigeraSystemContext sistema,
		ILogger<CorretoresController> logger)
	{
		_corretores = corretores;
		_orgaos = orgaos;
		_sistema = sistema;
		_logger = logger;
	}

	// O middleware de sistema já validou o cabeçalho e o acesso.
	private int Sistema => _sistema.SystemCode
		?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

	/// <summary>
	/// Pesquisa corretores (não excluídos).
	/// </summary>
	/// <remarks>
	/// `busca`: CPF ou CNPJ (com ou sem pontuação), código do corretor ou
	/// começo do nome. A pesquisa não traz a senha do representante.
	///
	///     GET /api/corretores?busca=MARIA&amp;cdFil=1&amp;tipoPessoa=F
	/// </remarks>
	/// <param name="busca">CPF, CNPJ, código ou começo do nome (opcional).</param>
	/// <param name="cdFil">Filial (opcional).</param>
	/// <param name="tipoPessoa">F (física) ou J (jurídica) (opcional).</param>
	/// <param name="cdSituacao">Situação (CD_SIT_CRRT) (opcional).</param>
	/// <param name="pagina">Página, a partir de 1.</param>
	/// <param name="tamanhoPagina">Itens por página, de 1 a 200.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Página de corretores ordenada pelo nome.</response>
	/// <response code="400">Tipo de pessoa ou paginação inválidos.</response>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Pesquisar(
		[FromQuery] string? busca,
		[FromQuery] decimal? cdFil,
		[FromQuery] string? tipoPessoa,
		[FromQuery] decimal? cdSituacao,
		[FromQuery] int pagina = 1,
		[FromQuery] int tamanhoPagina = 50,
		CancellationToken cancellationToken = default)
	{
		var resultado = await _corretores.PesquisarAsync(
			new PesquisaCorretoresRequest
			{
				Busca = busca,
				CdFil = cdFil,
				TipoPessoa = tipoPessoa,
				CdSituacao = cdSituacao,
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
			quantidade = resultado.Corretores.Count,
			corretores = resultado.Corretores
		});
	}

	/// <summary>
	/// Cadastro completo de um corretor.
	/// </summary>
	/// <remarks>
	/// `corretor.senhaRepresentante` (SH_RPR) vem em texto puro só para
	/// Administradores e Gestores; para os demais, vem nula.
	/// </remarks>
	/// <response code="200">Corretor, com pessoaFisica ou pessoaJuridica.</response>
	/// <response code="404">Não encontrado, excluído ou fora da regra física/jurídica.</response>
	[HttpGet("{sqPesCorretor:decimal}")]
	[ProducesResponseType(typeof(CorretorCadastroDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Obter(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var podeVerSenha = Grupos.PodemCadastrarCorretores.Any(User.IsInRole);
		var corretor = await _corretores.ObterAsync(sqPesCorretor, podeVerSenha, cancellationToken);

		return corretor is null
			? this.Erro(StatusCodes.Status404NotFound, "Corretor não encontrado.")
			: Ok(new { corretor });
	}

	/// <summary>
	/// Inclui um corretor (T000PESS + T000PFIS ou T000PJUR + T009CRRT). Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// O corretor é pessoa física ou jurídica, nunca as duas: `tipoPessoa` F
	/// leva só `pessoaFisica` (CPF obrigatório); J leva só `pessoaJuridica`
	/// (CNPJ obrigatório).
	///
	/// CPF ou CNPJ de uma pessoa que já existe mas ainda não é corretor (por
	/// exemplo, um associado): a pessoa é aproveitada e só o T009CRRT é criado.
	/// Os dados pessoais já gravados (nome inclusive) não mudam; só os campos
	/// vazios são completados com o que foi enviado. Já corretor: 409.
	/// A API preenche o código (SQ_PES = MAX + 1), a data e o usuário.
	///
	///     POST /api/corretores
	///     {
	///       "tipoPessoa": "F",
	///       "nome": "JOAO CORRETOR",
	///       "email": "joao@exemplo.com",
	///       "pessoaFisica": { "cpf": "529.982.247-25", "sexo": "M" },
	///       "corretor": { "cdFil": 1, "cdSituacao": 1, "comissaoAdesao": 10, "senhaRepresentante": "1234" }
	///     }
	///
	///     POST /api/corretores
	///     {
	///       "tipoPessoa": "J",
	///       "nome": "CORRETORA EXEMPLO LTDA",
	///       "nomeCurto": "EXEMPLO",
	///       "pessoaJuridica": { "cnpj": "11.222.333/0001-81", "contato": "MARIA", "cdCargo": 1 },
	///       "corretor": { "cdFil": 1 }
	///     }
	/// </remarks>
	/// <response code="201">Corretor cadastrado; devolve o cadastro com o sqPesCorretor gerado.</response>
	/// <response code="400">Dados inválidos ou referência inexistente.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="409">Documento de um corretor que já existe (ou excluído pela API), ou pessoa com cadastro incoerente.</response>
	[HttpPost]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CorretorCadastroDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Incluir([FromBody] CadastroCorretorDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _corretores.IncluirAsync(dados, Login(), cancellationToken);
		Registrar(resultado.Status, "Corretor incluído", resultado.Item?.SqPesCorretor);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um corretor (envie o cadastro inteiro). Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// O tipo de pessoa não muda (400). Bloco `corretor` omitido: T009CRRT
	/// fica como está. `corretor.senhaRepresentante` omitida mantém a atual;
	/// `""` apaga.
	/// </remarks>
	/// <response code="200">Corretor alterado.</response>
	/// <response code="400">Dados inválidos, referência inexistente ou troca de tipo.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Não encontrado.</response>
	/// <response code="409">CPF ou CNPJ já cadastrado em outra pessoa.</response>
	[HttpPut("{sqPesCorretor:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(CorretorCadastroDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Alterar(decimal sqPesCorretor, [FromBody] CadastroCorretorDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _corretores.AlterarAsync(sqPesCorretor, dados, cancellationToken);
		Registrar(resultado.Status, "Corretor alterado", sqPesCorretor);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui o corretor (exclusão lógica). Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Grava data e usuário em T009CRRT (DT_EXC_API e NM_USU_EXC_API); nada é
	/// apagado e a situação não muda. O corretor deixa de aparecer na API
	/// (pesquisa, concessão de acesso, vínculo de associado novo); o sistema
	/// legado continua mostrando.
	/// </remarks>
	/// <response code="200">Corretor excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Não encontrado.</response>
	[HttpDelete("{sqPesCorretor:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Excluir(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var resultado = await _corretores.ExcluirAsync(sqPesCorretor, Login(), cancellationToken);
		Registrar(resultado.Status, "Corretor excluído", sqPesCorretor);
		return this.Converter(resultado);
	}

	// ===================== Tabela de apoio: cargos =====================
	// T000CARG é a mesma tabela de /api/orgaos/cargos (cargo do vínculo do
	// associado); aqui ela serve ao cargo do contato do corretor pessoa
	// jurídica (T000PJUR.CD_CG). Mesmo serviço, mesmas regras.

	/// <summary>
	/// Cargos (T000CARG), para o campo pessoaJuridica.cdCargo do corretor.
	/// </summary>
	/// <param name="busca">Começo da descrição ou código (opcional). Até 200 itens.</param>
	/// <param name="cancellationToken">Cancelamento da requisição.</param>
	/// <response code="200">Cargos, com situação.</response>
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
	/// `cdCargo` omitido: MAX + 1. `descricao` até 30. `cdSituacao`: 1 = ATIVO,
	/// 0 = INATIVO (omitida: 1). É a mesma tabela de /api/orgaos/cargos.
	///
	///     POST /api/corretores/cargos
	///     { "descricao": "DIRETOR COMERCIAL", "cdSituacao": 1 }
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
		Registrar(resultado.Status, "Cargo incluído", resultado.Item?.CdCargo);
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
		Registrar(resultado.Status, "Cargo alterado", cdCargo);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um cargo. Somente administradores.
	/// </summary>
	/// <remarks>Recusada (409) se houver vínculos com órgão ou corretores pessoa jurídica com o cargo.</remarks>
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
		Registrar(resultado.Status, "Cargo excluído", cdCargo);
		return this.Converter(resultado);
	}

	private string Login() =>
		User.FindFirstValue(ClaimTypes.Email)
		?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
		?? User.Identity?.Name
		?? "api";

	// A senha do representante nunca vai para o log.
	private void Registrar(StatusTabelaApoio status, string operacao, decimal? codigo)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: código {Codigo} por {Usuario}.",
				operacao,
				codigo,
				Login());
		}
	}
}
