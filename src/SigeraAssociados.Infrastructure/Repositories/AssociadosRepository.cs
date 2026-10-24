using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Interfaces.Repositories;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Repositories;

/// <summary>
/// Implementa as consultas de associados utilizando Entity Framework Core.
/// O contexto é criado por operação, conforme o sistema selecionado
/// no cabeçalho da requisição. Toda consulta considera somente associados
/// com cadastro completo (T000PESS + T000CLIE + T000PFIS ou T000PJUR)
/// e é limitada pelo escopo
/// de acesso do usuário (empresa, filial e carteira do corretor).
/// A validação dos parâmetros é responsabilidade do AssociadosService.
/// </summary>
public sealed class AssociadosRepository : IAssociadosRepository
{
	// Multiplicadores usados para compor chaves numéricas únicas
	// a partir de CD_EMP numeric(2,0), CD_FIL numeric(2,0)
	// e SQ_PES numeric(7,0). Permitem traduzir vários pares
	// (empresa, filial) em um único IN (...) no SQL.
	private const decimal FatorFilial = 100m;
	private const decimal FatorPessoa = 10_000_000m;

	// Tipos de pessoa em T000PESS.TP_PES.
	private const string TipoPessoaFisica = "F";
	private const string TipoPessoaJuridica = "J";

	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _systemContext;

	public AssociadosRepository(
		SigeraDbContextFactory factory,
		SigeraSystemContext systemContext)
	{
		_factory = factory;
		_systemContext = systemContext;
	}

	/// <summary>
	/// Cria o contexto do banco correspondente à requisição atual.
	/// </summary>
	private SigeraDbContext CreateDbContext()
	{
		if (!_systemContext.SystemCode.HasValue)
		{
			throw new InvalidOperationException(
				"O sistema SIGERA não foi selecionado para esta requisição.");
		}

		return _factory.Create(_systemContext.SystemCode.Value);
	}

	/// <summary>
	/// Converte a entidade de associado para o DTO da API.
	/// Não expõe campos sensíveis do cadastro legado.
	/// </summary>
	private static IQueryable<AssociadoDto> ProjetarAssociados(
		IQueryable<T000CLIE> query)
	{
		return query.Select(a => new AssociadoDto
		{
			CDEmp = a.CD_EMP,
			SQPesCli = a.SQ_PES_CLI,
			Nome = a.Pessoa != null ? a.Pessoa.NM_PES : null,
			TipoPessoa = a.Pessoa != null ? a.Pessoa.TP_PES : null,
			CDSitCli = a.CD_SIT_CLI,
			Situacao = a.Situacao != null
				? a.Situacao.DE_SIT_CLI
				: null,
			CDFil = a.CD_FIL,
			DTCarteira = a.DT_CARTEIRA
		});
	}

	/// <summary>
	/// Mantém somente associados com cadastro completo.
	///
	/// Regra obrigatória: todo associado é pessoa física ou jurídica
	/// e precisa existir em T000PESS e T000CLIE e, conforme o tipo,
	/// em T000PFIS (TP_PES = 'F') ou T000PJUR (TP_PES = 'J').
	/// Equivale ao INNER JOIN com UNION ALL por tipo de pessoa,
	/// escrito com EXISTS para não duplicar linhas na paginação.
	/// </summary>
	internal static IQueryable<T000CLIE> FiltrarCadastroCompleto(
		IQueryable<T000CLIE> associados,
		IQueryable<T000PESS> pessoas,
		IQueryable<T000PFIS> pessoasFisicas,
		IQueryable<T000PJUR> pessoasJuridicas)
	{
		return associados.Where(a => pessoas.Any(p =>
			p.CD_EMP == a.CD_EMP
			&& p.SQ_PES == a.SQ_PES_CLI
			&& ((p.TP_PES == TipoPessoaFisica
					&& pessoasFisicas.Any(f =>
						f.CD_EMP == a.CD_EMP && f.SQ_PES_FIS == a.SQ_PES_CLI))
				|| (p.TP_PES == TipoPessoaJuridica
					&& pessoasJuridicas.Any(j =>
						j.CD_EMP == a.CD_EMP && j.SQ_PES_JUR == a.SQ_PES_CLI)))));
	}

	/// <summary>
	/// Associados válidos do banco atual, sem rastreamento.
	/// Ponto de partida de todas as consultas de associados.
	/// </summary>
	private static IQueryable<T000CLIE> AssociadosValidos(SigeraDbContext dbContext)
	{
		return FiltrarCadastroCompleto(
			dbContext.Associados.AsNoTracking(),
			dbContext.Pessoas.AsNoTracking(),
			dbContext.PessoasFisicas.AsNoTracking(),
			dbContext.PessoasJuridicas.AsNoTracking());
	}

	/// <summary>
	/// Restringe a consulta aos associados permitidos pelo escopo.
	///
	/// Regras (os escopos se somam):
	/// - empresa inteira: CD_EMP da lista;
	/// - empresa + filial: par (CD_EMP, CD_FIL) da lista;
	///   associados sem filial (CD_FIL nulo) não são incluídos;
	/// - carteira do corretor: existe contrato em T009CTRT
	///   do associado com o corretor, na empresa (e filial, se informada).
	/// </summary>
	internal static IQueryable<T000CLIE> AplicarEscopo(
		IQueryable<T000CLIE> query,
		IQueryable<T009CTRT> contratos,
		EscopoConsulta escopo)
	{
		ArgumentNullException.ThrowIfNull(escopo);

		if (escopo.AcessoTotal)
		{
			return query;
		}

		if (escopo.SemAcesso)
		{
			return query.Where(_ => false);
		}

		var empresasInteiras = escopo.Escopos
			.Where(e => e.SqPesCorretor is null && e.CdFil is null && e.CdEmp is not null)
			.Select(e => e.CdEmp!.Value)
			.Distinct()
			.ToList();

		var empresasFiliais = escopo.Escopos
			.Where(e => e.SqPesCorretor is null && e.CdFil is not null && e.CdEmp is not null)
			.Select(e => e.CdEmp!.Value * FatorFilial + e.CdFil!.Value)
			.Distinct()
			.ToList();

		// Carteira em toda a empresa: chave (empresa, corretor).
		var corretoresEmpresa = escopo.Escopos
			.Where(e => e.SqPesCorretor is not null && e.CdFil is null && e.CdEmp is not null)
			.Select(e => e.CdEmp!.Value * FatorPessoa + e.SqPesCorretor!.Value)
			.Distinct()
			.ToList();

		// Carteira restrita a uma filial do associado:
		// chave (empresa, filial, corretor).
		var corretoresFilial = escopo.Escopos
			.Where(e => e.SqPesCorretor is not null && e.CdFil is not null && e.CdEmp is not null)
			.Select(e => (e.CdEmp!.Value * FatorFilial + e.CdFil!.Value) * FatorPessoa
				+ e.SqPesCorretor!.Value)
			.Distinct()
			.ToList();

		var possuiCarteira =
			corretoresEmpresa.Count > 0 || corretoresFilial.Count > 0;

		return query.Where(a =>
			empresasInteiras.Contains(a.CD_EMP)
			|| (a.CD_FIL != null
				&& empresasFiliais.Contains(a.CD_EMP * FatorFilial + a.CD_FIL.Value))
			|| (possuiCarteira
				&& contratos.Any(c =>
					c.CD_EMP == a.CD_EMP
					&& c.SQ_PES_CLI == a.SQ_PES_CLI
					&& (corretoresEmpresa.Contains(c.CD_EMP * FatorPessoa + c.SQ_PES_CRO)
						|| (a.CD_FIL != null
							&& corretoresFilial.Contains(
								(a.CD_EMP * FatorFilial + a.CD_FIL.Value) * FatorPessoa
								+ c.SQ_PES_CRO))))));
	}

	/// <summary>
	/// Lista os primeiros dez associados em ordem determinística.
	/// </summary>
	public async Task<IReadOnlyList<AssociadoDto>> ListarAsync(
		EscopoConsulta escopo,
		CancellationToken cancellationToken = default)
	{
		using var dbContext = CreateDbContext();

		var query = AplicarEscopo(
			AssociadosValidos(dbContext),
			dbContext.Contratos.AsNoTracking(),
			escopo);

		return await ProjetarAssociados(
				query
					.OrderBy(a => a.CD_EMP)
					.ThenBy(a => a.SQ_PES_CLI))
			.Take(10)
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	/// Consulta um associado pela chave composta.
	/// Retorna null quando não encontrado ou fora do escopo.
	/// </summary>
	public async Task<AssociadoDto?> ConsultarPorCodigoAsync(
		decimal cdEmp,
		decimal sqPesCli,
		EscopoConsulta escopo,
		CancellationToken cancellationToken = default)
	{
		using var dbContext = CreateDbContext();

		var query = AplicarEscopo(
			AssociadosValidos(dbContext)
				.Where(a =>
					a.CD_EMP == cdEmp &&
					a.SQ_PES_CLI == sqPesCli),
			dbContext.Contratos.AsNoTracking(),
			escopo);

		return await ProjetarAssociados(query)
			.SingleOrDefaultAsync(cancellationToken);
	}

	/// <summary>
	/// Consulta associados com filtros, paginação e ordenação.
	/// Os parâmetros já chegam validados e normalizados pelo serviço.
	/// </summary>
	public async Task<ConsultaAssociadosResultado> ConsultarPaginadoAsync(
		ConsultaAssociadosRequest request,
		EscopoConsulta escopo,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		var campo = request.OrdenarPor;
		var direcao = request.Direcao;

		using var dbContext = CreateDbContext();

		var query = AplicarEscopo(
			AssociadosValidos(dbContext),
			dbContext.Contratos.AsNoTracking(),
			escopo);

		// Os filtros do consumidor atuam somente dentro do escopo.
		if (request.CdEmp.HasValue)
		{
			query = query.Where(
				a => a.CD_EMP == request.CdEmp.Value);
		}

		if (!string.IsNullOrWhiteSpace(request.Nome))
		{
			var nome = request.Nome.Trim();

			query = query.Where(a =>
				a.Pessoa != null &&
				a.Pessoa.NM_PES != null &&
				a.Pessoa.NM_PES.Contains(nome));
		}

		if (request.CdSitCli.HasValue)
		{
			query = query.Where(
				a => a.CD_SIT_CLI == request.CdSitCli.Value);
		}

		var totalRegistros = await query.CountAsync(cancellationToken);

		// A ordenação principal respeita o campo e a direção solicitados.
		IOrderedQueryable<T000CLIE> ordenada = campo switch
		{
			"nome" when direcao == "desc" =>
				query.OrderByDescending(a => a.Pessoa!.NM_PES),
			"nome" =>
				query.OrderBy(a => a.Pessoa!.NM_PES),

			"cdemp" when direcao == "desc" =>
				query.OrderByDescending(a => a.CD_EMP),
			"cdemp" =>
				query.OrderBy(a => a.CD_EMP),

			"sqpescli" when direcao == "desc" =>
				query.OrderByDescending(a => a.SQ_PES_CLI),
			"sqpescli" =>
				query.OrderBy(a => a.SQ_PES_CLI),

			"cdsitcli" when direcao == "desc" =>
				query.OrderByDescending(a => a.CD_SIT_CLI),
			"cdsitcli" =>
				query.OrderBy(a => a.CD_SIT_CLI),

			"situacao" when direcao == "desc" =>
				query.OrderByDescending(a => a.Situacao!.DE_SIT_CLI),
			"situacao" =>
				query.OrderBy(a => a.Situacao!.DE_SIT_CLI),

			"dtcarteira" when direcao == "desc" =>
				query.OrderByDescending(a => a.DT_CARTEIRA),
			"dtcarteira" =>
				query.OrderBy(a => a.DT_CARTEIRA),

			_ => throw new ArgumentException(
				"Campo de ordenação inválido.")
		};

		// Mantém os critérios de desempate para estabilizar a paginação.
		var associados = await ProjetarAssociados(
				ordenada
					.ThenBy(a => a.CD_EMP)
					.ThenBy(a => a.SQ_PES_CLI))
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.ToListAsync(cancellationToken);

		var totalPaginas = (int)Math.Ceiling(
			totalRegistros / (double)request.TamanhoPagina);

		return new ConsultaAssociadosResultado
		{
			CdEmp = request.CdEmp,
			Nome = request.Nome,
			CdSitCli = request.CdSitCli,
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = totalRegistros,
			TotalPaginas = totalPaginas,
			CampoOrdenacao = campo,
			Direcao = direcao,
			Quantidade = associados.Count,
			Associados = associados
		};
	}
}
