using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Orgaos;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Infrastructure.Persistence;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Orgaos;

/// <summary>
/// Órgãos (T000ORGA), grupos (T009GRUP), subórgãos (T009SBOR), lotações
/// (T000LOTA), cargos (T000CARG) e situações do vínculo (T000SPOG)
/// no banco do sistema informado.
/// </summary>
public sealed class OrgaosService : IOrgaosService
{
	private const int TamanhoPaginaMaximo = 200;

	private readonly SigeraDbContextFactory _factory;

	public OrgaosService(SigeraDbContextFactory factory)
	{
		_factory = factory;
	}

	// ===================== Órgãos =====================

	/// <inheritdoc />
	public async Task<ConsultaOrgaosResultado> ListarAsync(
		int sistema,
		ConsultaOrgaosRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		ValidarSistema(sistema);

		if (request.Pagina < 1)
		{
			throw new DadosInvalidosException("A página deve ser maior ou igual a 1.");
		}

		if (request.TamanhoPagina < 1 || request.TamanhoPagina > TamanhoPaginaMaximo)
		{
			throw new DadosInvalidosException($"O tamanho da página deve estar entre 1 e {TamanhoPaginaMaximo}.");
		}

		await using var db = _factory.Create(sistema);

		var query = db.Orgaos.AsNoTracking();

		if (request.CdFil is decimal filial)
		{
			query = query.Where(o => o.CD_FIL == filial);
		}

		if (request.CdSituacao is decimal situacao)
		{
			if (!SituacaoOrgao.Valida(situacao))
			{
				throw new DadosInvalidosException("A situação do órgão deve ser 1 (ATIVO) ou 0 (INATIVO).");
			}

			query = query.Where(o => o.CD_SIT_ORG == situacao);
		}

		if (!string.IsNullOrWhiteSpace(request.Uf))
		{
			var uf = request.Uf.Trim().ToUpperInvariant();
			query = query.Where(o => o.SG_UF == uf);
		}

		if (!string.IsNullOrWhiteSpace(request.Busca))
		{
			var busca = request.Busca.Trim();

			query = decimal.TryParse(busca, out var codigo) && codigo % 1 == 0
				? query.Where(o => o.CD_ORG == codigo || o.NM_ORG.StartsWith(busca))
				: query.Where(o => o.NM_ORG.StartsWith(busca) || (o.SGL_ORG != null && o.SGL_ORG.StartsWith(busca)));
		}

		var total = await query.CountAsync(cancellationToken);

		var orgaos = await query
			.OrderBy(o => o.NM_ORG)
			.ThenBy(o => o.CD_ORG)
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.Select(o => new OrgaoResumoDto
			{
				CdOrg = o.CD_ORG,
				Nome = o.NM_ORG,
				Sigla = o.SGL_ORG,
				Uf = o.SG_UF,
				CdFil = o.CD_FIL,
				CdSituacao = o.CD_SIT_ORG
			})
			.ToListAsync(cancellationToken);

		// Descrição fixa (sem tabela no banco), preenchida depois da consulta.
		orgaos = orgaos
			.Select(o => new OrgaoResumoDto
			{
				CdOrg = o.CdOrg,
				Nome = o.Nome,
				Sigla = o.Sigla,
				Uf = o.Uf,
				CdFil = o.CdFil,
				CdSituacao = o.CdSituacao,
				Situacao = SituacaoOrgao.Descricao(o.CdSituacao)
			})
			.ToList();

		return new ConsultaOrgaosResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = total,
			TotalPaginas = (int)Math.Ceiling(total / (double)request.TamanhoPagina),
			Orgaos = orgaos
		};
	}

	/// <inheritdoc />
	public async Task<OrgaoDados?> ObterAsync(int sistema, decimal cdOrg, CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var orgao = await db.Orgaos.AsNoTracking().FirstOrDefaultAsync(o => o.CD_ORG == cdOrg, cancellationToken);

		return orgao is null ? null : ParaDados(orgao);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<OrgaoDados>> IncluirAsync(
		int sistema,
		OrgaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Orgao(dados, inclusao: true) is string erro)
		{
			return Invalido<OrgaoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<OrgaoDados>();
		}

		if (await ValidarReferenciasAsync(db, emp, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<OrgaoDados>(erroReferencia);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdOrg ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_ORG), 0) + 1 AS [Value] FROM dbo.T000ORGA WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > 999_999)
		{
			return Invalido<OrgaoDados>("Não há código de órgão livre (o máximo é 999999).");
		}

		if (await db.Orgaos.AnyAsync(o => o.CD_EMP == emp && o.CD_ORG == codigo, cancellationToken))
		{
			return Conflito<OrgaoDados>($"O órgão {codigo:0} já existe.");
		}

		var orgao = new T000ORGA { CD_EMP = emp, CD_ORG = codigo };
		Aplicar(dados, orgao);

		// Órgão novo sem situação informada: ATIVO.
		orgao.CD_SIT_ORG ??= SituacaoOrgao.Ativo;
		db.Orgaos.Add(orgao);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<OrgaoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Órgão cadastrado.", ParaDados(orgao));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<OrgaoDados>> AlterarAsync(
		int sistema,
		decimal cdOrg,
		OrgaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Orgao(dados, inclusao: false) is string erro)
		{
			return Invalido<OrgaoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var orgao = await db.Orgaos.FirstOrDefaultAsync(o => o.CD_ORG == cdOrg, cancellationToken);

		if (orgao is null)
		{
			return NaoEncontrado<OrgaoDados>("Órgão não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, orgao.CD_EMP, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<OrgaoDados>(erroReferencia);
		}

		Aplicar(dados, orgao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<OrgaoDados>(erroGravacao)
			: Sucesso("Órgão alterado.", ParaDados(orgao));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<OrgaoDados>> ExcluirAsync(
		int sistema,
		decimal cdOrg,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var orgao = await db.Orgaos.FirstOrDefaultAsync(o => o.CD_ORG == cdOrg, cancellationToken);

		if (orgao is null)
		{
			return NaoEncontrado<OrgaoDados>("Órgão não encontrado.");
		}

		if (await db.VinculosOrgao.AnyAsync(v => v.CD_EMP == orgao.CD_EMP && v.CD_ORG == cdOrg, cancellationToken))
		{
			return Conflito<OrgaoDados>($"O órgão {cdOrg:0} não pode ser excluído: há associados vinculados a ele.");
		}

		var dto = ParaDados(orgao);
		db.Orgaos.Remove(orgao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<OrgaoDados>(erroGravacao)
			: Sucesso("Órgão excluído.", dto);
	}

	// ===================== Lotações =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<LotacaoDados>> ListarLotacoesAsync(
		int sistema,
		string? busca,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var query = db.Lotacoes.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(l => l.CD_LOT == codigo || l.DE_LOT.StartsWith(texto))
				: query.Where(l => l.DE_LOT.StartsWith(texto));
		}

		return await query
			.OrderBy(l => l.DE_LOT)
			.ThenBy(l => l.CD_LOT)
			.Take(TamanhoPaginaMaximo)
			.Select(l => new LotacaoDados { CdLot = l.CD_LOT, Descricao = l.DE_LOT, CdSituacao = l.CD_SIT_LOT })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<LotacaoDados>> IncluirLotacaoAsync(
		int sistema,
		LotacaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Lotacao(dados, inclusao: true) is string erro)
		{
			return Invalido<LotacaoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<LotacaoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdLot ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_LOT), 0) + 1 AS [Value] FROM dbo.T000LOTA WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > 999_999)
		{
			return Invalido<LotacaoDados>("Não há código de lotação livre (o máximo é 999999).");
		}

		if (await db.Lotacoes.AnyAsync(l => l.CD_EMP == emp && l.CD_LOT == codigo, cancellationToken))
		{
			return Conflito<LotacaoDados>($"A lotação {codigo:0} já existe.");
		}

		var lotacao = new T000LOTA
		{
			CD_EMP = emp,
			CD_LOT = codigo,
			DE_LOT = dados.Descricao!.Trim(),
			CD_SIT_LOT = dados.CdSituacao
		};

		db.Lotacoes.Add(lotacao);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<LotacaoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Lotação cadastrada.", ParaDados(lotacao));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<LotacaoDados>> AlterarLotacaoAsync(
		int sistema,
		decimal cdLot,
		LotacaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Lotacao(dados, inclusao: false) is string erro)
		{
			return Invalido<LotacaoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var lotacao = await db.Lotacoes.FirstOrDefaultAsync(l => l.CD_LOT == cdLot, cancellationToken);

		if (lotacao is null)
		{
			return NaoEncontrado<LotacaoDados>("Lotação não encontrada.");
		}

		lotacao.DE_LOT = dados.Descricao!.Trim();
		lotacao.CD_SIT_LOT = dados.CdSituacao;
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Lotação alterada.", ParaDados(lotacao));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<LotacaoDados>> ExcluirLotacaoAsync(
		int sistema,
		decimal cdLot,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var lotacao = await db.Lotacoes.FirstOrDefaultAsync(l => l.CD_LOT == cdLot, cancellationToken);

		if (lotacao is null)
		{
			return NaoEncontrado<LotacaoDados>("Lotação não encontrada.");
		}

		if (await db.VinculosOrgao.AnyAsync(v => v.CD_EMP == lotacao.CD_EMP && v.CD_LOT == cdLot, cancellationToken))
		{
			return Conflito<LotacaoDados>($"A lotação {cdLot:0} não pode ser excluída: há vínculos com ela.");
		}

		var dto = ParaDados(lotacao);
		db.Lotacoes.Remove(lotacao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<LotacaoDados>(erroGravacao)
			: Sucesso("Lotação excluída.", dto);
	}

	// ===================== Grupos =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<GrupoDados>> ListarGruposAsync(int sistema, CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var grupos = await db.Grupos
			.AsNoTracking()
			.OrderBy(g => g.CD_GP)
			.Select(g => new { g.CD_GP, g.DE_GP, g.TP_GP, g.ID_ORD, g.NR_DD })
			.ToListAsync(cancellationToken);

		// Descrições fixas (sem tabela no banco), preenchidas depois da consulta.
		return grupos
			.Select(g => ParaDados(g.CD_GP, g.DE_GP, g.TP_GP, g.ID_ORD, g.NR_DD))
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<GrupoDados>> IncluirGrupoAsync(
		int sistema,
		GrupoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Grupo(dados, inclusao: true) is string erro)
		{
			return Invalido<GrupoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<GrupoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdGrupo ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_GP), 0) + 1 AS [Value] FROM dbo.T009GRUP WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > 999)
		{
			return Invalido<GrupoDados>("Não há código de grupo livre (o máximo é 999).");
		}

		if (await db.Grupos.AnyAsync(g => g.CD_EMP == emp && g.CD_GP == codigo, cancellationToken))
		{
			return Conflito<GrupoDados>($"O grupo {codigo:0} já existe.");
		}

		var grupo = new T009GRUP { CD_EMP = emp, CD_GP = codigo };
		Aplicar(dados, grupo);
		db.Grupos.Add(grupo);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<GrupoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Grupo cadastrado.", ParaDados(grupo));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<GrupoDados>> AlterarGrupoAsync(
		int sistema,
		decimal cdGrupo,
		GrupoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Grupo(dados, inclusao: false) is string erro)
		{
			return Invalido<GrupoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var grupo = await db.Grupos.FirstOrDefaultAsync(g => g.CD_GP == cdGrupo, cancellationToken);

		if (grupo is null)
		{
			return NaoEncontrado<GrupoDados>("Grupo não encontrado.");
		}

		Aplicar(dados, grupo);
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Grupo alterado.", ParaDados(grupo));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<GrupoDados>> ExcluirGrupoAsync(
		int sistema,
		decimal cdGrupo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var grupo = await db.Grupos.FirstOrDefaultAsync(g => g.CD_GP == cdGrupo, cancellationToken);

		if (grupo is null)
		{
			return NaoEncontrado<GrupoDados>("Grupo não encontrado.");
		}

		if (await db.Orgaos.AnyAsync(o => o.CD_EMP == grupo.CD_EMP && o.CD_GP == cdGrupo, cancellationToken))
		{
			return Conflito<GrupoDados>($"O grupo {cdGrupo:0} não pode ser excluído: há órgãos nele.");
		}

		var dto = ParaDados(grupo);
		db.Grupos.Remove(grupo);

		// Outras tabelas do legado podem usar o grupo: a FK do banco barra (409).
		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<GrupoDados>(erroGravacao)
			: Sucesso("Grupo excluído.", dto);
	}

	// ===================== Subórgãos =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<SubOrgaoDados>> ListarSubOrgaosAsync(int sistema, CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.SubOrgaos
			.AsNoTracking()
			.OrderBy(s => s.NM_SBOR)
			.Select(s => new SubOrgaoDados { SqSubOrgao = s.SQ_SBOR, Nome = s.NM_SBOR })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<SubOrgaoDados>> IncluirSubOrgaoAsync(
		int sistema,
		SubOrgaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.SubOrgao(dados) is string erro)
		{
			return Invalido<SubOrgaoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<SubOrgaoDados>();
		}

		var nome = dados.Nome!.Trim();

		// SQ_SBOR é IDENTITY: o banco gera o código.
		var codigo = await NumeroAsync(db,
			$"INSERT INTO dbo.T009SBOR (NM_SBOR, CD_EMP) VALUES ({nome}, {emp}); SELECT CAST(SCOPE_IDENTITY() AS decimal(3, 0)) AS [Value]",
			cancellationToken);

		return Sucesso("Subórgão cadastrado.", new SubOrgaoDados { SqSubOrgao = codigo, Nome = nome });
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<SubOrgaoDados>> AlterarSubOrgaoAsync(
		int sistema,
		decimal sqSubOrgao,
		SubOrgaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.SubOrgao(dados) is string erro)
		{
			return Invalido<SubOrgaoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var subOrgao = await db.SubOrgaos.FirstOrDefaultAsync(s => s.SQ_SBOR == sqSubOrgao, cancellationToken);

		if (subOrgao is null)
		{
			return NaoEncontrado<SubOrgaoDados>("Subórgão não encontrado.");
		}

		subOrgao.NM_SBOR = dados.Nome!.Trim();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Subórgão alterado.", new SubOrgaoDados { SqSubOrgao = subOrgao.SQ_SBOR, Nome = subOrgao.NM_SBOR });
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<SubOrgaoDados>> ExcluirSubOrgaoAsync(
		int sistema,
		decimal sqSubOrgao,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var subOrgao = await db.SubOrgaos.FirstOrDefaultAsync(s => s.SQ_SBOR == sqSubOrgao, cancellationToken);

		if (subOrgao is null)
		{
			return NaoEncontrado<SubOrgaoDados>("Subórgão não encontrado.");
		}

		if (await db.Orgaos.AnyAsync(o => o.CD_EMP == subOrgao.CD_EMP && o.SQ_SBOR == sqSubOrgao, cancellationToken))
		{
			return Conflito<SubOrgaoDados>($"O subórgão {sqSubOrgao:0} não pode ser excluído: há órgãos ligados a ele.");
		}

		var dto = new SubOrgaoDados { SqSubOrgao = subOrgao.SQ_SBOR, Nome = subOrgao.NM_SBOR };
		db.SubOrgaos.Remove(subOrgao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<SubOrgaoDados>(erroGravacao)
			: Sucesso("Subórgão excluído.", dto);
	}

	// ===================== Cargos =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<CargoDados>> ListarCargosAsync(
		int sistema,
		string? busca,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var query = db.Cargos.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(c => c.CD_CG == codigo || c.DE_CG.StartsWith(texto))
				: query.Where(c => c.DE_CG.StartsWith(texto));
		}

		return await query
			.OrderBy(c => c.DE_CG)
			.ThenBy(c => c.CD_CG)
			.Take(TamanhoPaginaMaximo)
			.Select(c => new CargoDados { CdCargo = c.CD_CG, Descricao = c.DE_CG, CdSituacao = c.CD_SIT_CAG })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CargoDados>> IncluirCargoAsync(
		int sistema,
		CargoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Cargo(dados, inclusao: true) is string erro)
		{
			return Invalido<CargoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<CargoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdCargo ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_CG), 0) + 1 AS [Value] FROM dbo.T000CARG WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > 9_999)
		{
			return Invalido<CargoDados>("Não há código de cargo livre (o máximo é 9999).");
		}

		if (await db.Cargos.AnyAsync(c => c.CD_EMP == emp && c.CD_CG == codigo, cancellationToken))
		{
			return Conflito<CargoDados>($"O cargo {codigo:0} já existe.");
		}

		var cargo = new T000CARG
		{
			CD_EMP = emp,
			CD_CG = codigo,
			DE_CG = dados.Descricao!.Trim(),
			CD_SIT_CAG = dados.CdSituacao
		};

		db.Cargos.Add(cargo);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<CargoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Cargo cadastrado.", ParaDados(cargo));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CargoDados>> AlterarCargoAsync(
		int sistema,
		decimal cdCargo,
		CargoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Cargo(dados, inclusao: false) is string erro)
		{
			return Invalido<CargoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var cargo = await db.Cargos.FirstOrDefaultAsync(c => c.CD_CG == cdCargo, cancellationToken);

		if (cargo is null)
		{
			return NaoEncontrado<CargoDados>("Cargo não encontrado.");
		}

		cargo.DE_CG = dados.Descricao!.Trim();
		cargo.CD_SIT_CAG = dados.CdSituacao;
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Cargo alterado.", ParaDados(cargo));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CargoDados>> ExcluirCargoAsync(
		int sistema,
		decimal cdCargo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var cargo = await db.Cargos.FirstOrDefaultAsync(c => c.CD_CG == cdCargo, cancellationToken);

		if (cargo is null)
		{
			return NaoEncontrado<CargoDados>("Cargo não encontrado.");
		}

		if (await db.VinculosOrgao.AnyAsync(v => v.CD_EMP == cargo.CD_EMP && v.CD_CG == cdCargo, cancellationToken))
		{
			return Conflito<CargoDados>($"O cargo {cdCargo:0} não pode ser excluído: há vínculos com ele.");
		}

		var dto = ParaDados(cargo);
		db.Cargos.Remove(cargo);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<CargoDados>(erroGravacao)
			: Sucesso("Cargo excluído.", dto);
	}

	// ===================== Situações do vínculo =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<ItemTabelaDto>> ListarSituacoesAsync(int sistema, CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		return await db.SituacoesVinculo
			.AsNoTracking()
			.OrderBy(s => s.CD_SIT_ORG)
			.Select(s => new ItemTabelaDto { Codigo = s.CD_SIT_ORG, Descricao = s.DE_SIT_ORG })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirSituacaoAsync(
		int sistema,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoOrgaos.TamanhoSituacaoVinculo, inclusao: true) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ItemTabelaDto>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_SIT_ORG), 0) + 1 AS [Value] FROM dbo.T000SPOG WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > 99)
		{
			return Invalido<ItemTabelaDto>("Não há código de situação livre (o máximo é 99).");
		}

		if (await db.SituacoesVinculo.AnyAsync(s => s.CD_EMP == emp && s.CD_SIT_ORG == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"A situação {codigo:0} já existe.");
		}

		var situacao = new T000SPOG { CD_EMP = emp, CD_SIT_ORG = codigo, DE_SIT_ORG = dados.Descricao!.Trim() };
		db.SituacoesVinculo.Add(situacao);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ItemTabelaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Situação cadastrada.", new ItemTabelaDto { Codigo = codigo, Descricao = situacao.DE_SIT_ORG });
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarSituacaoAsync(
		int sistema,
		decimal codigo,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoOrgaos.TamanhoSituacaoVinculo, inclusao: false) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var situacao = await db.SituacoesVinculo.FirstOrDefaultAsync(s => s.CD_SIT_ORG == codigo, cancellationToken);

		if (situacao is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Situação não encontrada.");
		}

		situacao.DE_SIT_ORG = dados.Descricao!.Trim();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Situação alterada.", new ItemTabelaDto { Codigo = codigo, Descricao = situacao.DE_SIT_ORG });
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirSituacaoAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var situacao = await db.SituacoesVinculo.FirstOrDefaultAsync(s => s.CD_SIT_ORG == codigo, cancellationToken);

		if (situacao is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Situação não encontrada.");
		}

		if (await db.VinculosOrgao.AnyAsync(v => v.CD_EMP == situacao.CD_EMP && v.CD_SIT_ORG == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"A situação {codigo:0} não pode ser excluída: há vínculos nela.");
		}

		var dto = new ItemTabelaDto { Codigo = codigo, Descricao = situacao.DE_SIT_ORG };
		db.SituacoesVinculo.Remove(situacao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ItemTabelaDto>(erroGravacao)
			: Sucesso("Situação excluída.", dto);
	}

	// ===================== Apoio =====================

	/// <summary>
	/// Confere filial (T000PARA), grupo (T009GRUP), subórgão (T009SBOR)
	/// e municípios (T000MUNI).
	/// </summary>
	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		OrgaoDados dados,
		CancellationToken cancellationToken)
	{
		if (dados.CdFil is decimal filial
			&& !await db.Filiais.AnyAsync(f => f.CD_EMP == emp && f.CD_FIL == filial, cancellationToken))
		{
			return $"A filial {filial:0} não está cadastrada (T000PARA).";
		}

		if (dados.CdGrupo is decimal grupo
			&& !await db.Grupos.AnyAsync(g => g.CD_EMP == emp && g.CD_GP == grupo, cancellationToken))
		{
			return $"O grupo {grupo:0} não está cadastrado (T009GRUP).";
		}

		if (dados.SqSubOrgao is decimal subOrgao
			&& !await db.SubOrgaos.AnyAsync(s => s.CD_EMP == emp && s.SQ_SBOR == subOrgao, cancellationToken))
		{
			return $"O subórgão {subOrgao:0} não está cadastrado (T009SBOR).";
		}

		foreach (var municipio in new[] { dados.Endereco?.CdMunicipio, dados.EmpresaProcessadora?.Endereco?.CdMunicipio })
		{
			if (municipio is decimal cdMun
				&& !await db.Municipios.AnyAsync(m => m.CD_EMP == emp && m.CD_MUN == cdMun, cancellationToken))
			{
				return $"O município {cdMun:0} não está cadastrado (T000MUNI).";
			}
		}

		return null;
	}

	private static void Aplicar(OrgaoDados d, T000ORGA o)
	{
		o.NM_ORG = d.Nome!.Trim();
		o.SGL_ORG = Texto(d.Sigla);
		o.NR_ORG = Texto(d.Numero);
		o.CD_GP = d.CdGrupo;
		o.SQ_SBOR = d.SqSubOrgao;
		o.SG_UF = Texto(d.Uf)?.ToUpperInvariant();
		o.CD_FIL = d.CdFil;
		o.CD_SIT_ORG = d.CdSituacao;
		o.DT_LIM_PRO = d.DataLimiteProcessamento;
		o.DT_PAG = d.DataPagamento;

		o.NM_LOG_ORG = Texto(d.Endereco?.Logradouro);
		o.NR_LOG_ORG = d.Endereco?.Numero;
		o.CP_EN_ORG = Texto(d.Endereco?.Complemento);
		o.NM_BAI_ORG = Texto(d.Endereco?.Bairro);
		o.NR_CEP_ORG = Numero(d.Endereco?.Cep);
		o.CD_MUN_ORG = d.Endereco?.CdMunicipio;

		o.NM_CON_ADM = Texto(d.ContatoAdministrativo?.Nome);
		o.NR_FN_ADM = Numero(d.ContatoAdministrativo?.Telefone);
		o.NM_EML_ADM = Texto(d.ContatoAdministrativo?.Email);

		o.NM_CON_FIN = Texto(d.ContatoFinanceiro?.Nome);
		o.NR_FN_FIN = Numero(d.ContatoFinanceiro?.Telefone);
		o.NM_EML_FIN = Texto(d.ContatoFinanceiro?.Email);

		var ep = d.EmpresaProcessadora;
		o.NM_EMP_PRO = Texto(ep?.Nome);
		o.NM_CON_EP = Texto(ep?.Contato);
		o.NR_FN_EP = Numero(ep?.Telefone);
		o.NM_EML_EP = Texto(ep?.Email);
		o.NM_LOG_EP = Texto(ep?.Endereco?.Logradouro);
		o.NR_LOG_EP = ep?.Endereco?.Numero;
		o.CP_EN_EP = Texto(ep?.Endereco?.Complemento);
		o.NM_BAI_EP = Texto(ep?.Endereco?.Bairro);
		o.NR_CEP_EP = Numero(ep?.Endereco?.Cep);
		o.CD_MUN_EP = ep?.Endereco?.CdMunicipio;
	}

	private static OrgaoDados ParaDados(T000ORGA o) => new()
	{
		CdOrg = o.CD_ORG,
		Nome = o.NM_ORG,
		Sigla = o.SGL_ORG,
		Numero = o.NR_ORG,
		CdGrupo = o.CD_GP,
		SqSubOrgao = o.SQ_SBOR,
		Uf = o.SG_UF,
		CdFil = o.CD_FIL,
		CdSituacao = o.CD_SIT_ORG,
		Situacao = SituacaoOrgao.Descricao(o.CD_SIT_ORG),
		DataLimiteProcessamento = o.DT_LIM_PRO,
		DataPagamento = o.DT_PAG,
		Endereco = new EnderecoDados
		{
			Logradouro = o.NM_LOG_ORG,
			Numero = o.NR_LOG_ORG,
			Complemento = o.CP_EN_ORG,
			Bairro = o.NM_BAI_ORG,
			Cep = Digitos(o.NR_CEP_ORG, 8),
			CdMunicipio = o.CD_MUN_ORG
		},
		ContatoAdministrativo = new ContatoDados { Nome = o.NM_CON_ADM, Telefone = Digitos(o.NR_FN_ADM), Email = o.NM_EML_ADM },
		ContatoFinanceiro = new ContatoDados { Nome = o.NM_CON_FIN, Telefone = Digitos(o.NR_FN_FIN), Email = o.NM_EML_FIN },
		EmpresaProcessadora = new EmpresaProcessadoraDados
		{
			Nome = o.NM_EMP_PRO,
			Contato = o.NM_CON_EP,
			Telefone = Digitos(o.NR_FN_EP),
			Email = o.NM_EML_EP,
			Endereco = new EnderecoDados
			{
				Logradouro = o.NM_LOG_EP,
				Numero = o.NR_LOG_EP,
				Complemento = o.CP_EN_EP,
				Bairro = o.NM_BAI_EP,
				Cep = Digitos(o.NR_CEP_EP, 8),
				CdMunicipio = o.CD_MUN_EP
			}
		}
	};

	private static void Aplicar(GrupoDados d, T009GRUP g)
	{
		g.DE_GP = d.Descricao!.Trim();
		g.TP_GP = RegrasGrupoOrgao.Normalizar(d.Tipo)!;
		g.ID_ORD = RegrasGrupoOrgao.Normalizar(d.Ordem)!;
		g.NR_DD = d.NumeroDias!.Value;
	}

	private static GrupoDados ParaDados(T009GRUP g) =>
		ParaDados(g.CD_GP, g.DE_GP, g.TP_GP, g.ID_ORD, g.NR_DD);

	private static GrupoDados ParaDados(decimal codigo, string descricao, string tipo, string ordem, decimal dias) => new()
	{
		CdGrupo = codigo,
		Descricao = descricao,
		Tipo = tipo,
		DescricaoTipo = RegrasGrupoOrgao.DescricaoTipo(tipo),
		Ordem = ordem,
		DescricaoOrdem = RegrasGrupoOrgao.DescricaoOrdem(ordem),
		NumeroDias = dias
	};

	private static CargoDados ParaDados(T000CARG c) => new() { CdCargo = c.CD_CG, Descricao = c.DE_CG, CdSituacao = c.CD_SIT_CAG };

	private static LotacaoDados ParaDados(T000LOTA l) => new() { CdLot = l.CD_LOT, Descricao = l.DE_LOT, CdSituacao = l.CD_SIT_LOT };

	private static void ValidarSistema(int sistema)
	{
		if (!SigeraSystemCatalog.TryGetDatabase(sistema, out _))
		{
			throw new DadosInvalidosException("Sistema inválido. Os valores permitidos são 1, 2, 3 e 4.");
		}
	}
}
