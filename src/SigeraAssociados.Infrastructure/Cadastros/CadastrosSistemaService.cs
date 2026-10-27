using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Cadastros;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Cadastros;

/// <summary>
/// Lê as tabelas de apoio no banco do sistema informado: filiais (T000PARA),
/// corretores (T009CRRT + T000PESS), situações (T000SCLI),
/// tratamentos (T000TRTA) e municípios (T000MUNI). Somente leitura.
/// Cada base tem uma única empresa, então não há filtro por empresa.
/// </summary>
public sealed class CadastrosSistemaService : ICadastrosSistemaService
{
	internal const int TamanhoPaginaMaximo = 200;

	private readonly SigeraDbContextFactory _factory;

	public CadastrosSistemaService(SigeraDbContextFactory factory)
	{
		_factory = factory;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<FilialDto>> ListarFiliaisAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.Filiais
			.AsNoTracking()
			.OrderBy(f => f.CD_FIL)
			.Select(f => new FilialDto
			{
				CdFil = f.CD_FIL,
				Nome = f.NM_EMP,
				NomeCurto = f.NC_EMP,
				Municipio = f.DE_MUN_EMP,
				Uf = f.SG_UF_EMP
			})
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ConsultaCorretoresResultado> ListarCorretoresAsync(
		int sistema,
		ConsultaCorretoresRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		ValidarSistema(sistema);

		if (request.CdFil is not null
			&& (request.CdFil < 1 || request.CdFil > 99 || request.CdFil % 1 != 0))
		{
			throw new DadosInvalidosException("A filial deve ser um código inteiro entre 1 e 99.");
		}

		ValidarPaginacao(request.Pagina, request.TamanhoPagina);

		await using var db = _factory.Create(sistema);

		var query =
			from c in db.Corretores.AsNoTracking()
			where c.DT_EXC_API == null // corretor excluído pela API não aparece
			join p in db.Pessoas.AsNoTracking()
				on new { Empresa = c.CD_EMP, Pessoa = c.SQ_PES_CRO }
				equals new { Empresa = p.CD_EMP, Pessoa = p.SQ_PES }
			select new CorretorDto
			{
				SqPesCorretor = c.SQ_PES_CRO,
				Nome = p.NM_PES,
				CdFil = c.CD_FIL,
				CdSituacao = c.CD_SIT_CRRT
			};

		if (request.CdFil is decimal filial)
		{
			query = query.Where(c => c.CdFil == filial);
		}

		if (!string.IsNullOrWhiteSpace(request.Busca))
		{
			var busca = request.Busca.Trim();

			// Número: código exato ou começo do nome. Texto: começo do nome
			// (NM_PES LIKE 'nome%', que usa o índice de nome em T000PESS).
			query = decimal.TryParse(busca, out var codigo) && codigo % 1 == 0
				? query.Where(c => c.SqPesCorretor == codigo || c.Nome.StartsWith(busca))
				: query.Where(c => c.Nome.StartsWith(busca));
		}

		var totalRegistros = await query.CountAsync(cancellationToken);

		var corretores = await query
			.OrderBy(c => c.Nome)
			.ThenBy(c => c.SqPesCorretor)
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.ToListAsync(cancellationToken);

		return new ConsultaCorretoresResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = totalRegistros,
			TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)request.TamanhoPagina),
			Corretores = corretores
		};
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ItemTabelaDto>> ListarSituacoesAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.SituacoesAssociados
			.AsNoTracking()
			.OrderBy(s => s.CD_SIT_CLI)
			.Select(s => new ItemTabelaDto { Codigo = s.CD_SIT_CLI, Descricao = s.DE_SIT_CLI })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ItemTabelaDto>> ListarTratamentosAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.Tratamentos
			.AsNoTracking()
			.OrderBy(t => t.CD_TRT)
			.Select(t => new ItemTabelaDto { Codigo = t.CD_TRT, Descricao = t.DE_TRT })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<BancoDados>> ListarBancosAsync(
		int sistema,
		string? busca,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var query = db.Bancos.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(b => b.CD_BCO == codigo || b.NM_BCO.StartsWith(texto))
				: query.Where(b => b.NM_BCO.StartsWith(texto));
		}

		var bancos = await query
			.OrderBy(b => b.CD_BCO)
			.Select(b => new { b.CD_BCO, b.NM_BCO, b.CD_SIT_BCO })
			.ToListAsync(cancellationToken);

		// Descrição da situação é fixa (sem tabela): preenchida depois da consulta.
		return bancos
			.Select(b => new BancoDados
			{
				CdBanco = b.CD_BCO,
				Nome = b.NM_BCO,
				CdSituacao = b.CD_SIT_BCO,
				Situacao = SituacaoAtivoInativo.Banco.Descricao(b.CD_SIT_BCO)
			})
			.ToList();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ItemTabelaDto>> ListarTiposEnderecoAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.TiposEndereco
			.AsNoTracking()
			.OrderBy(t => t.CD_TP_EN)
			.Select(t => new ItemTabelaDto { Codigo = t.CD_TP_EN, Descricao = t.DE_TP_EN })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ItemTabelaDto>> ListarTiposTelefoneAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.TiposTelefone
			.AsNoTracking()
			.OrderBy(t => t.CD_TP_FN)
			.Select(t => new ItemTabelaDto { Codigo = t.CD_TP_FN, Descricao = t.DE_TP_FN })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ConsultaMunicipiosResultado> ListarMunicipiosAsync(
		int sistema,
		ConsultaMunicipiosRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		ValidarSistema(sistema);

		string? uf = null;

		if (!string.IsNullOrWhiteSpace(request.Uf))
		{
			uf = request.Uf.Trim().ToUpperInvariant();

			if (uf.Length != 2 || !uf.All(char.IsAsciiLetterUpper))
			{
				throw new DadosInvalidosException("A UF deve ter 2 letras (ex.: CE).");
			}
		}

		ValidarPaginacao(request.Pagina, request.TamanhoPagina);

		await using var db = _factory.Create(sistema);

		var query = db.Municipios.AsNoTracking();

		if (uf is not null)
		{
			query = query.Where(m => m.SG_UF == uf);
		}

		if (!string.IsNullOrWhiteSpace(request.Busca))
		{
			var busca = request.Busca.Trim();

			query = decimal.TryParse(busca, out var codigo) && codigo % 1 == 0
				? query.Where(m => m.CD_MUN == codigo)
				: query.Where(m => m.DE_MUN.StartsWith(busca));
		}

		var totalRegistros = await query.CountAsync(cancellationToken);

		var municipios = await query
			.OrderBy(m => m.DE_MUN)
			.ThenBy(m => m.SG_UF)
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.Select(m => new MunicipioDto { CdMun = m.CD_MUN, Nome = m.DE_MUN, Uf = m.SG_UF })
			.ToListAsync(cancellationToken);

		return new ConsultaMunicipiosResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = totalRegistros,
			TotalPaginas = (int)Math.Ceiling(totalRegistros / (double)request.TamanhoPagina),
			Municipios = municipios
		};
	}

	private static void ValidarPaginacao(int pagina, int tamanhoPagina)
	{
		if (pagina < 1)
		{
			throw new DadosInvalidosException("A página deve ser maior ou igual a 1.");
		}

		if (tamanhoPagina < 1 || tamanhoPagina > TamanhoPaginaMaximo)
		{
			throw new DadosInvalidosException(
				$"O tamanho da página deve estar entre 1 e {TamanhoPaginaMaximo}.");
		}
	}

	private static void ValidarSistema(int sistema)
	{
		if (!SigeraSystemCatalog.TryGetDatabase(sistema, out _))
		{
			throw new DadosInvalidosException(
				"Sistema inválido. Os valores permitidos são 1, 2, 3 e 4.");
		}
	}
}
