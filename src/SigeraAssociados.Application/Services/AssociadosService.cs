using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Associados;
using SigeraAssociados.Application.Interfaces.Repositories;

namespace SigeraAssociados.Application.Services;

/// <summary>
/// Coordena as consultas de associados, valida os parâmetros
/// e aplica o escopo de acesso do usuário atual,
/// delegando a persistência ao repositório.
/// </summary>
public sealed class AssociadosService : IAssociadosService
{
	private static readonly string[] CamposPermitidos =
	[
		"nome",
		"cdemp",
		"sqpescli",
		"cdsitcli",
		"situacao",
		"dtcarteira"
	];

	private readonly IAssociadosRepository _repository;
	private readonly IEscopoAcessoService _escopoAcesso;

	public AssociadosService(
		IAssociadosRepository repository,
		IEscopoAcessoService escopoAcesso)
	{
		_repository = repository;
		_escopoAcesso = escopoAcesso;
	}

	/// <summary>
	/// Lista os primeiros dez associados dentro do escopo do usuário.
	/// </summary>
	public async Task<IReadOnlyList<AssociadoDto>> ListarAsync(
		CancellationToken cancellationToken = default)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso)
		{
			return Array.Empty<AssociadoDto>();
		}

		return await _repository.ListarAsync(escopo, cancellationToken);
	}

	/// <summary>
	/// Consulta um associado pela chave composta.
	/// Retorna null quando não encontrado ou fora do escopo do usuário.
	/// </summary>
	public async Task<AssociadoDto?> ConsultarPorCodigoAsync(
		decimal cdEmp,
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso)
		{
			return null;
		}

		return await _repository.ConsultarPorCodigoAsync(
			cdEmp,
			sqPesCli,
			escopo,
			cancellationToken);
	}

	/// <summary>
	/// Valida os filtros, a paginação e a ordenação antes de consultar.
	/// </summary>
	public async Task<ConsultaAssociadosResultado> ConsultarPaginadoAsync(
		ConsultaAssociadosRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.Pagina < 1)
		{
			throw new DadosInvalidosException(
				"A página deve ser maior ou igual a 1.");
		}

		if (request.TamanhoPagina < 1 || request.TamanhoPagina > 100)
		{
			throw new DadosInvalidosException(
				"O tamanho da página deve estar entre 1 e 100.");
		}

		var campo = (request.OrdenarPor ?? "nome")
			.Trim()
			.ToLowerInvariant();

		var direcao = (request.Direcao ?? "asc")
			.Trim()
			.ToLowerInvariant();

		if (!CamposPermitidos.Contains(campo))
		{
			throw new DadosInvalidosException(
				"Campo de ordenação inválido.",
				new Dictionary<string, object?>
				{
					["camposPermitidos"] = CamposPermitidos
				});
		}

		if (direcao is not ("asc" or "desc"))
		{
			throw new DadosInvalidosException(
				"A direção deve ser 'asc' ou 'desc'.");
		}

		// Encaminha a consulta com os valores normalizados.
		var consultaNormalizada = new ConsultaAssociadosRequest
		{
			CdEmp = request.CdEmp,
			Nome = request.Nome,
			CdSitCli = request.CdSitCli,
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			OrdenarPor = campo,
			Direcao = direcao
		};

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso)
		{
			return new ConsultaAssociadosResultado
			{
				CdEmp = consultaNormalizada.CdEmp,
				Nome = consultaNormalizada.Nome,
				CdSitCli = consultaNormalizada.CdSitCli,
				Pagina = consultaNormalizada.Pagina,
				TamanhoPagina = consultaNormalizada.TamanhoPagina,
				TotalRegistros = 0,
				TotalPaginas = 0,
				CampoOrdenacao = campo,
				Direcao = direcao,
				Quantidade = 0,
				Associados = Array.Empty<AssociadoDto>()
			};
		}

		return await _repository.ConsultarPaginadoAsync(
			consultaNormalizada,
			escopo,
			cancellationToken);
	}
}
