using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.ContasBancarias;
using SigeraAssociados.Application.Enderecos;
using SigeraAssociados.Application.Interfaces.ContasBancarias;
using SigeraAssociados.Application.Interfaces.Enderecos;
using SigeraAssociados.Application.Interfaces.Pix;
using SigeraAssociados.Application.Interfaces.Referencias;
using SigeraAssociados.Application.Interfaces.Telefones;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Application.Pix;
using SigeraAssociados.Application.Referencias;
using SigeraAssociados.Application.Telefones;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Controllers;

/// <summary>
/// Endereços, telefones, contas bancárias, referências e chaves Pix do
/// corretor, com as mesmas regras e tabelas do associado (os dados são da
/// pessoa: quem é associado e corretor vê os mesmos nas duas telas).
/// Consultar: qualquer usuário com acesso ao sistema.
/// Incluir, alterar e excluir: Administradores e Gestores (como o cadastro do corretor).
/// Corretor inexistente, excluído ou fora da regra física/jurídica: 404.
/// </summary>
[ApiController]
[Route("api/corretores/{sqPesCorretor:decimal}")]
[Authorize]
public sealed class CorretorContatosController : ControllerBase
{
	private const TitularCadastro Titular = TitularCadastro.Corretor;

	private readonly IEnderecosAssociadoService _enderecos;
	private readonly ITelefonesAssociadoService _telefones;
	private readonly IContasBancariasService _contas;
	private readonly IReferenciasAssociadoService _referencias;
	private readonly IChavesPixService _pix;
	private readonly ILogger<CorretorContatosController> _logger;

	public CorretorContatosController(
		IEnderecosAssociadoService enderecos,
		ITelefonesAssociadoService telefones,
		IContasBancariasService contas,
		IReferenciasAssociadoService referencias,
		IChavesPixService pix,
		ILogger<CorretorContatosController> logger)
	{
		_enderecos = enderecos;
		_telefones = telefones;
		_contas = contas;
		_referencias = referencias;
		_pix = pix;
		_logger = logger;
	}

	// ===================== Endereços =====================

	/// <summary>
	/// Lista os endereços do corretor.
	/// </summary>
	/// <response code="200">Lista (pode vir vazia).</response>
	/// <response code="404">Corretor não encontrado.</response>
	[HttpGet("enderecos")]
	[ProducesResponseType(typeof(IReadOnlyList<EnderecoAssociadoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListarEnderecos(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var itens = await _enderecos.ListarAsync(Titular, sqPesCorretor, cancellationToken);

		return itens is null
			? this.Erro(StatusCodes.Status404NotFound, "Corretor não encontrado.")
			: Ok(new { sqPesCorretor, quantidade = itens.Count, enderecos = itens });
	}

	/// <summary>
	/// Inclui um endereço no corretor. Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Mesmas regras do endereço do associado: `sqEndereco` = MAX + 1 da pessoa (até 99); sempre um único principal (o primeiro é principal; marcar outro desmarca os demais). Obrigatórios: `cdTipo` (GET /api/cadastros/tipos-endereco), `logradouro` e `cdMunicipio`.
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPost("enderecos")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(EnderecoAssociadoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirEndereco(decimal sqPesCorretor, [FromBody] EnderecoAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _enderecos.IncluirAsync(Titular, sqPesCorretor, dados, cancellationToken);
		Registrar(resultado.Status, "Endereço incluído", sqPesCorretor, resultado.Item?.SqEndereco);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um endereço do corretor (envie todos os campos). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPut("enderecos/{sqEndereco:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(EnderecoAssociadoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AlterarEndereco(decimal sqPesCorretor, decimal sqEndereco, [FromBody] EnderecoAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _enderecos.AlterarAsync(Titular, sqPesCorretor, sqEndereco, dados, cancellationToken);
		Registrar(resultado.Status, "Endereço alterado", sqPesCorretor, sqEndereco);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um endereço do corretor (exclusão física). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	[HttpDelete("enderecos/{sqEndereco:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ExcluirEndereco(decimal sqPesCorretor, decimal sqEndereco, CancellationToken cancellationToken)
	{
		var resultado = await _enderecos.ExcluirAsync(Titular, sqPesCorretor, sqEndereco, cancellationToken);
		Registrar(resultado.Status, "Endereço excluído", sqPesCorretor, sqEndereco);
		return this.Converter(resultado);
	}

	// ===================== Telefones =====================

	/// <summary>
	/// Lista os telefones do corretor.
	/// </summary>
	/// <response code="200">Lista (pode vir vazia).</response>
	/// <response code="404">Corretor não encontrado.</response>
	[HttpGet("telefones")]
	[ProducesResponseType(typeof(IReadOnlyList<TelefoneAssociadoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListarTelefones(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var itens = await _telefones.ListarAsync(Titular, sqPesCorretor, cancellationToken);

		return itens is null
			? this.Erro(StatusCodes.Status404NotFound, "Corretor não encontrado.")
			: Ok(new { sqPesCorretor, quantidade = itens.Count, telefones = itens });
	}

	/// <summary>
	/// Inclui um telefone no corretor. Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Mesmas regras do telefone do associado: `ddd` (11 a 99) e `numero` (8 ou 9 números) obrigatórios; `cdTipo` opcional (GET /api/cadastros/tipos-telefone). Mesmo DDD e número repetido: 409.
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPost("telefones")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(TelefoneAssociadoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirTelefone(decimal sqPesCorretor, [FromBody] TelefoneAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _telefones.IncluirAsync(Titular, sqPesCorretor, dados, cancellationToken);
		Registrar(resultado.Status, "Telefone incluído", sqPesCorretor, resultado.Item?.SqTelefone);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera um telefone do corretor (envie todos os campos). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPut("telefones/{sqTelefone:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(TelefoneAssociadoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AlterarTelefone(decimal sqPesCorretor, decimal sqTelefone, [FromBody] TelefoneAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _telefones.AlterarAsync(Titular, sqPesCorretor, sqTelefone, dados, cancellationToken);
		Registrar(resultado.Status, "Telefone alterado", sqPesCorretor, sqTelefone);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui um telefone do corretor (exclusão física). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	[HttpDelete("telefones/{sqTelefone:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ExcluirTelefone(decimal sqPesCorretor, decimal sqTelefone, CancellationToken cancellationToken)
	{
		var resultado = await _telefones.ExcluirAsync(Titular, sqPesCorretor, sqTelefone, cancellationToken);
		Registrar(resultado.Status, "Telefone excluído", sqPesCorretor, sqTelefone);
		return this.Converter(resultado);
	}

	// ===================== Contas bancárias =====================

	/// <summary>
	/// Lista as contas bancárias do corretor.
	/// </summary>
	/// <response code="200">Lista (pode vir vazia).</response>
	/// <response code="404">Corretor não encontrado.</response>
	[HttpGet("contas")]
	[ProducesResponseType(typeof(IReadOnlyList<ContaBancariaDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListarContas(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var itens = await _contas.ListarAsync(Titular, sqPesCorretor, cancellationToken);

		return itens is null
			? this.Erro(StatusCodes.Status404NotFound, "Corretor não encontrado.")
			: Ok(new { sqPesCorretor, quantidade = itens.Count, contas = itens });
	}

	/// <summary>
	/// Inclui uma conta bancária no corretor. Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Mesmas regras da conta do associado: `sqConta` = MAX + 1 da pessoa (até 99); banco (GET /api/cadastros/bancos), agência e número obrigatórios; mesma conta repetida: 409.
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPost("contas")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ContaBancariaDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirConta(decimal sqPesCorretor, [FromBody] ContaBancariaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _contas.IncluirAsync(Titular, sqPesCorretor, dados, cancellationToken);
		Registrar(resultado.Status, "Conta bancária incluída", sqPesCorretor, resultado.Item?.SqConta);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma conta bancária do corretor (envie todos os campos). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPut("contas/{sqConta:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ContaBancariaDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AlterarConta(decimal sqPesCorretor, decimal sqConta, [FromBody] ContaBancariaDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _contas.AlterarAsync(Titular, sqPesCorretor, sqConta, dados, cancellationToken);
		Registrar(resultado.Status, "Conta bancária alterada", sqPesCorretor, sqConta);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma conta bancária do corretor (exclusão física). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	[HttpDelete("contas/{sqConta:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ExcluirConta(decimal sqPesCorretor, decimal sqConta, CancellationToken cancellationToken)
	{
		var resultado = await _contas.ExcluirAsync(Titular, sqPesCorretor, sqConta, cancellationToken);
		Registrar(resultado.Status, "Conta bancária excluída", sqPesCorretor, sqConta);
		return this.Converter(resultado);
	}

	// ===================== Referências =====================

	/// <summary>
	/// Lista as referências do corretor.
	/// </summary>
	/// <response code="200">Lista (pode vir vazia).</response>
	/// <response code="404">Corretor não encontrado.</response>
	[HttpGet("referencias")]
	[ProducesResponseType(typeof(IReadOnlyList<ReferenciaAssociadoDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListarReferencias(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var itens = await _referencias.ListarAsync(Titular, sqPesCorretor, cancellationToken);

		return itens is null
			? this.Erro(StatusCodes.Status404NotFound, "Corretor não encontrado.")
			: Ok(new { sqPesCorretor, quantidade = itens.Count, referencias = itens });
	}

	/// <summary>
	/// Inclui uma referência no corretor. Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Mesmas regras da referência do associado: `nome` e `telefone1` obrigatórios; `dataNascimento` e `telefone2` opcionais. Mesmo nome e telefone 1 repetidos: 409.
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPost("referencias")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ReferenciaAssociadoDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirReferencia(decimal sqPesCorretor, [FromBody] ReferenciaAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _referencias.IncluirAsync(Titular, sqPesCorretor, dados, cancellationToken);
		Registrar(resultado.Status, "Referência incluída", sqPesCorretor, resultado.Item?.SqReferencia);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma referência do corretor (envie todos os campos). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPut("referencias/{sqReferencia:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ReferenciaAssociadoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AlterarReferencia(decimal sqPesCorretor, decimal sqReferencia, [FromBody] ReferenciaAssociadoDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _referencias.AlterarAsync(Titular, sqPesCorretor, sqReferencia, dados, cancellationToken);
		Registrar(resultado.Status, "Referência alterada", sqPesCorretor, sqReferencia);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma referência do corretor (exclusão física). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	[HttpDelete("referencias/{sqReferencia:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ExcluirReferencia(decimal sqPesCorretor, decimal sqReferencia, CancellationToken cancellationToken)
	{
		var resultado = await _referencias.ExcluirAsync(Titular, sqPesCorretor, sqReferencia, cancellationToken);
		Registrar(resultado.Status, "Referência excluída", sqPesCorretor, sqReferencia);
		return this.Converter(resultado);
	}

	// ===================== Chaves Pix =====================

	/// <summary>
	/// Lista as chaves Pix do corretor.
	/// </summary>
	/// <response code="200">Lista (pode vir vazia).</response>
	/// <response code="404">Corretor não encontrado.</response>
	[HttpGet("pix")]
	[ProducesResponseType(typeof(IReadOnlyList<ChavePixDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListarPix(decimal sqPesCorretor, CancellationToken cancellationToken)
	{
		var itens = await _pix.ListarAsync(Titular, sqPesCorretor, cancellationToken);

		return itens is null
			? this.Erro(StatusCodes.Status404NotFound, "Corretor não encontrado.")
			: Ok(new { sqPesCorretor, quantidade = itens.Count, chaves = itens });
	}

	/// <summary>
	/// Inclui uma chave Pix no corretor. Administradores e Gestores.
	/// </summary>
	/// <remarks>
	/// Mesmas regras da chave Pix do associado: `cdTipo` 1 = CPF/CNPJ, 2 = CELULAR (gravado sem +55), 3 = E-MAIL, 4 = ALEATÓRIA; a chave é validada pelo tipo. Mesma chave repetida: 409.
	/// </remarks>
	/// <response code="201">Incluído.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPost("pix")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ChavePixDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> IncluirPix(decimal sqPesCorretor, [FromBody] ChavePixDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _pix.IncluirAsync(Titular, sqPesCorretor, dados, cancellationToken);
		Registrar(resultado.Status, "Chave Pix incluída", sqPesCorretor, resultado.Item?.SqPix);
		return this.Converter(resultado, criado: true);
	}

	/// <summary>
	/// Altera uma chave Pix do corretor (envie todos os campos). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Alterado.</response>
	/// <response code="400">Dados inválidos.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	/// <response code="409">Já cadastrado no corretor.</response>
	[HttpPut("pix/{sqPix:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[Consumes("application/json")]
	[ProducesResponseType(typeof(ChavePixDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AlterarPix(decimal sqPesCorretor, decimal sqPix, [FromBody] ChavePixDados dados, CancellationToken cancellationToken)
	{
		var resultado = await _pix.AlterarAsync(Titular, sqPesCorretor, sqPix, dados, cancellationToken);
		Registrar(resultado.Status, "Chave Pix alterada", sqPesCorretor, sqPix);
		return this.Converter(resultado);
	}

	/// <summary>
	/// Exclui uma chave Pix do corretor (exclusão física). Administradores e Gestores.
	/// </summary>
	/// <response code="200">Excluído.</response>
	/// <response code="403">Não é Administrador nem Gestor.</response>
	/// <response code="404">Corretor ou registro não encontrado.</response>
	[HttpDelete("pix/{sqPix:decimal}")]
	[Authorize(Roles = Grupos.PerfisCadastroCorretores)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ExcluirPix(decimal sqPesCorretor, decimal sqPix, CancellationToken cancellationToken)
	{
		var resultado = await _pix.ExcluirAsync(Titular, sqPesCorretor, sqPix, cancellationToken);
		Registrar(resultado.Status, "Chave Pix excluída", sqPesCorretor, sqPix);
		return this.Converter(resultado);
	}

	// Só códigos vão para o log (nada de chave Pix, conta ou telefone).
	private void Registrar(StatusTabelaApoio status, string operacao, decimal sqPesCorretor, decimal? sequencia)
	{
		if (status == StatusTabelaApoio.Sucesso)
		{
			_logger.LogInformation(
				"{Operacao}: corretor {SqPesCorretor}, sequência {Sequencia}, por {Usuario}.",
				operacao,
				sqPesCorretor,
				sequencia,
				User.Identity?.Name ?? "(desconhecido)");
		}
	}
}
