using System.Linq.Expressions;
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
		IQueryable<T000CLIE> query,
		IQueryable<T000PFIS> pessoasFisicas)
	{
		return query.Select(a => new AssociadoDto
		{
			CDEmp = a.CD_EMP,
			SQPesCli = a.SQ_PES_CLI,
			Nome = a.Pessoa != null ? a.Pessoa.NM_PES : null,
			TipoPessoa = a.Pessoa != null ? a.Pessoa.TP_PES : null,
			// O CPF sai do banco e é formatado (completo) antes da resposta.
			Cpf = DocumentoCpf.Formatar(pessoasFisicas
				.Where(f => f.CD_EMP == a.CD_EMP && f.SQ_PES_FIS == a.SQ_PES_CLI)
				.Select(f => f.NR_CPF)
				.FirstOrDefault()),
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
	/// Filtra pelo CPF exato (T000PFIS.NR_CPF). O CPF chega validado
	/// pelo AssociadosService, só com os 11 números.
	/// Associados pessoa jurídica não têm CPF e ficam de fora.
	/// </summary>
	internal static IQueryable<T000CLIE> FiltrarPorCpf(
		IQueryable<T000CLIE> query,
		IQueryable<T000PFIS> pessoasFisicas,
		string? cpf)
	{
		if (string.IsNullOrWhiteSpace(cpf))
		{
			return query;
		}

		var numero = decimal.Parse(cpf, System.Globalization.CultureInfo.InvariantCulture);

		return query.Where(a => pessoasFisicas.Any(f =>
			f.CD_EMP == a.CD_EMP
			&& f.SQ_PES_FIS == a.SQ_PES_CLI
			&& f.NR_CPF == numero));
	}

	/// <summary>
	/// Filtra pelo começo do nome da pessoa (NM_PES LIKE 'nome%').
	/// Diferente de "contém" (LIKE '%nome%'), permite ao SQL Server usar
	/// um índice em NM_PES. Maiúsculas/minúsculas e acentos seguem a
	/// collation do banco.
	/// </summary>
	internal static IQueryable<T000CLIE> FiltrarPorNome(
		IQueryable<T000CLIE> query,
		string? nome)
	{
		if (string.IsNullOrWhiteSpace(nome))
		{
			return query;
		}

		var inicio = nome.Trim();

		return query.Where(a =>
			a.Pessoa != null &&
			a.Pessoa.NM_PES.StartsWith(inicio));
	}

	/// <summary>
	/// Associados válidos do banco atual, sem rastreamento.
	/// Ponto de partida de todas as consultas de associados.
	/// </summary>
	private static IQueryable<T000CLIE> AssociadosValidos(SigeraDbContext dbContext)
	{
		// Associados excluídos pela API (exclusão lógica) não aparecem.
		return FiltrarCadastroCompleto(
			dbContext.Associados.AsNoTracking().Where(a => a.DT_EXC_API == null),
			dbContext.Pessoas.AsNoTracking(),
			dbContext.PessoasFisicas.AsNoTracking(),
			dbContext.PessoasJuridicas.AsNoTracking());
	}

	/// <summary>
	/// Restringe a consulta aos associados permitidos pelo escopo.
	///
	/// Cada escopo vira uma condição própria, e as condições são somadas
	/// com OR:
	/// - empresa inteira: CD_EMP = empresa;
	/// - empresa + filial: CD_EMP = empresa AND CD_FIL = filial
	///   (associados sem filial não entram);
	/// - carteira do corretor: CD_EMP = empresa [AND CD_FIL = filial]
	///   AND (cadastrado pelo corretor (SQ_PES_CRO_CAD) OR EXISTS (contrato
	///   em T009CTRT do associado com o corretor)).
	///
	/// Cada valor é comparado com a sua própria coluna, com o mesmo tipo
	/// (numeric(2,0) ou numeric(7,0)). Isso evita chaves calculadas
	/// (ex.: CD_EMP * 100 + CD_FIL = 201), que estouravam o tipo do
	/// parâmetro, e evita OPENJSON nas bases com compatibilidade 100.
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

		Expression<Func<T000CLIE, bool>>? filtro = null;

		foreach (var item in escopo.Escopos.Where(e => e.CdEmp is not null).Distinct())
		{
			// Cópias locais: cada escopo vira parâmetros próprios no SQL.
			var empresa = item.CdEmp!.Value;
			var filial = item.CdFil;
			var corretor = item.SqPesCorretor;

			Expression<Func<T000CLIE, bool>> condicao;

			if (corretor is null && filial is null)
			{
				condicao = a => a.CD_EMP == empresa;
			}
			else if (corretor is null)
			{
				var filialValor = filial!.Value;
				condicao = a => a.CD_EMP == empresa && a.CD_FIL == filialValor;
			}
			else if (filial is null)
			{
				var corretorValor = corretor.Value;
				condicao = a => a.CD_EMP == empresa
					&& (a.SQ_PES_CRO_CAD == corretorValor
						|| contratos.Any(c =>
							c.CD_EMP == a.CD_EMP
							&& c.SQ_PES_CLI == a.SQ_PES_CLI
							&& c.SQ_PES_CRO == corretorValor));
			}
			else
			{
				var filialValor = filial.Value;
				var corretorValor = corretor.Value;
				condicao = a => a.CD_EMP == empresa
					&& a.CD_FIL == filialValor
					&& (a.SQ_PES_CRO_CAD == corretorValor
						|| contratos.Any(c =>
							c.CD_EMP == a.CD_EMP
							&& c.SQ_PES_CLI == a.SQ_PES_CLI
							&& c.SQ_PES_CRO == corretorValor));
			}

			filtro = filtro is null ? condicao : Ou(filtro, condicao);
		}

		return filtro is null
			? query.Where(_ => false)
			: query.Where(filtro);
	}

	/// <summary>
	/// Junta duas condições com OR, usando o mesmo parâmetro de entrada.
	/// </summary>
	internal static Expression<Func<T, bool>> Ou<T>(
		Expression<Func<T, bool>> primeira,
		Expression<Func<T, bool>> segunda)
	{
		var parametro = primeira.Parameters[0];

		var corpoSegunda = new SubstituirParametro(segunda.Parameters[0], parametro)
			.Visit(segunda.Body)!;

		return Expression.Lambda<Func<T, bool>>(
			Expression.OrElse(primeira.Body, corpoSegunda),
			parametro);
	}

	/// <summary>
	/// Troca o parâmetro de uma expressão por outro.
	/// </summary>
	private sealed class SubstituirParametro : ExpressionVisitor
	{
		private readonly ParameterExpression _de;
		private readonly ParameterExpression _para;

		public SubstituirParametro(ParameterExpression de, ParameterExpression para)
		{
			_de = de;
			_para = para;
		}

		protected override Expression VisitParameter(ParameterExpression node)
		{
			return node == _de ? _para : base.VisitParameter(node);
		}
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
			dbContext.Contratos.AsNoTracking().Where(c => c.DT_EXC_API == null),
			escopo);

		return await ProjetarAssociados(
				query
					.OrderBy(a => a.CD_EMP)
					.ThenBy(a => a.SQ_PES_CLI),
				dbContext.PessoasFisicas.AsNoTracking())
			.Take(10)
			.ToListAsync(cancellationToken);
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
			dbContext.Contratos.AsNoTracking().Where(c => c.DT_EXC_API == null),
			escopo);

		// Os filtros do consumidor atuam somente dentro do escopo.
		if (request.CdFil.HasValue)
		{
			query = query.Where(
				a => a.CD_FIL == request.CdFil.Value);
		}

		query = FiltrarPorNome(query, request.Nome);
		query = FiltrarPorCpf(query, dbContext.PessoasFisicas.AsNoTracking(), request.Cpf);

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

			"cdfil" when direcao == "desc" =>
				query.OrderByDescending(a => a.CD_FIL),
			"cdfil" =>
				query.OrderBy(a => a.CD_FIL),

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

			// Inalcançável: o AssociadosService já validou o campo.
			_ => throw new InvalidOperationException(
				$"Campo de ordenação não suportado: {campo}.")
		};

		// Mantém os critérios de desempate para estabilizar a paginação.
		var associados = await ProjetarAssociados(
				ordenada
					.ThenBy(a => a.CD_EMP)
					.ThenBy(a => a.SQ_PES_CLI),
				dbContext.PessoasFisicas.AsNoTracking())
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.ToListAsync(cancellationToken);

		var totalPaginas = (int)Math.Ceiling(
			totalRegistros / (double)request.TamanhoPagina);

		return new ConsultaAssociadosResultado
		{
			CdFil = request.CdFil,
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
