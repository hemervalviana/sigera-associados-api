using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Orgaos;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Infrastructure.Associados;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Orgaos;

/// <summary>
/// Vínculos do associado com órgãos (T000RPOR). SQ_ORG = MAX + 1 da pessoa,
/// com UPDLOCK, HOLDLOCK. O associado precisa estar no escopo do usuário.
/// </summary>
public sealed class VinculosOrgaoService : IVinculosOrgaoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public VinculosOrgaoService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<VinculoOrgaoDto>?> ListarAsync(
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return await Projetar(db, emp, sqPesCli).ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<VinculoOrgaoDto>> IncluirAsync(
		decimal sqPesCli,
		VinculoOrgaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Vinculo(dados) is string erro)
		{
			return Invalido<VinculoOrgaoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<VinculoOrgaoDto>("Associado não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, emp, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<VinculoOrgaoDto>(erroReferencia);
		}

		if (await MatriculaRepetidaAsync(db, emp, sqPesCli, null, dados, cancellationToken))
		{
			return Conflito<VinculoOrgaoDto>("O associado já tem um vínculo com este órgão e esta matrícula.");
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqOrg = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_ORG), 0) + 1 AS [Value] FROM dbo.T000RPOR WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND SQ_PES = {sqPesCli}",
			cancellationToken);

		if (sqOrg > 999)
		{
			return Invalido<VinculoOrgaoDto>("O associado atingiu o limite de 999 vínculos.");
		}

		var vinculo = new T000RPOR { CD_EMP = emp, SQ_PES = sqPesCli, SQ_ORG = sqOrg };
		Aplicar(dados, vinculo);
		db.VinculosOrgao.Add(vinculo);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<VinculoOrgaoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Vínculo incluído.", await ObterAsync(db, emp, sqPesCli, sqOrg, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<VinculoOrgaoDto>> AlterarAsync(
		decimal sqPesCli,
		decimal sqOrg,
		VinculoOrgaoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoOrgaos.Vinculo(dados) is string erro)
		{
			return Invalido<VinculoOrgaoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<VinculoOrgaoDto>("Associado não encontrado.");
		}

		var vinculo = await db.VinculosOrgao.FirstOrDefaultAsync(
			v => v.CD_EMP == emp && v.SQ_PES == sqPesCli && v.SQ_ORG == sqOrg,
			cancellationToken);

		if (vinculo is null)
		{
			return NaoEncontrado<VinculoOrgaoDto>("Vínculo não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, emp, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<VinculoOrgaoDto>(erroReferencia);
		}

		if (await MatriculaRepetidaAsync(db, emp, sqPesCli, sqOrg, dados, cancellationToken))
		{
			return Conflito<VinculoOrgaoDto>("O associado já tem outro vínculo com este órgão e esta matrícula.");
		}

		Aplicar(dados, vinculo);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<VinculoOrgaoDto>(erroGravacao)
			: Sucesso("Vínculo alterado.", await ObterAsync(db, emp, sqPesCli, sqOrg, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<VinculoOrgaoDto>> ExcluirAsync(
		decimal sqPesCli,
		decimal sqOrg,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<VinculoOrgaoDto>("Associado não encontrado.");
		}

		var dto = await ObterAsync(db, emp, sqPesCli, sqOrg, cancellationToken);

		var vinculo = await db.VinculosOrgao.FirstOrDefaultAsync(
			v => v.CD_EMP == emp && v.SQ_PES == sqPesCli && v.SQ_ORG == sqOrg,
			cancellationToken);

		if (vinculo is null)
		{
			return NaoEncontrado<VinculoOrgaoDto>("Vínculo não encontrado.");
		}

		db.VinculosOrgao.Remove(vinculo);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<VinculoOrgaoDto>(erroGravacao)
			: Sucesso("Vínculo excluído.", dto);
	}

	// ----- Apoio -----

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	/// <summary>
	/// Empresa da base, quando o associado existe e está no escopo do usuário.
	/// </summary>
	private async Task<decimal?> AbrirAssociadoAsync(
		SigeraDbContext db,
		decimal sqPesCli,
		CancellationToken cancellationToken)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso || await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return await AcessoAssociado.PermitidoAsync(db, emp, sqPesCli, escopo, cancellationToken)
			? emp
			: null;
	}

	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		VinculoOrgaoDados dados,
		CancellationToken cancellationToken)
	{
		var orgao = dados.CdOrg!.Value;

		if (!await db.Orgaos.AnyAsync(o => o.CD_EMP == emp && o.CD_ORG == orgao, cancellationToken))
		{
			return $"O órgão {orgao:0} não está cadastrado (T000ORGA).";
		}

		if (dados.CdLotacao is decimal lotacao
			&& !await db.Lotacoes.AnyAsync(l => l.CD_EMP == emp && l.CD_LOT == lotacao, cancellationToken))
		{
			return $"A lotação {lotacao:0} não está cadastrada (T000LOTA).";
		}

		if (dados.CdSituacao is decimal situacao
			&& !await db.SituacoesVinculo.AnyAsync(s => s.CD_EMP == emp && s.CD_SIT_ORG == situacao, cancellationToken))
		{
			return $"A situação {situacao:0} não está cadastrada (T000SPOG).";
		}

		if (dados.CdCargo is decimal cargo
			&& !await db.Cargos.AnyAsync(c => c.CD_EMP == emp && c.CD_CG == cargo, cancellationToken))
		{
			return $"O cargo {cargo:0} não está cadastrado (T000CARG).";
		}

		return null;
	}

	/// <summary>
	/// Mesmo órgão com a mesma matrícula já cadastrado para a pessoa.
	/// Sem matrícula, não há conferência (a pessoa pode ter mais de um vínculo no órgão).
	/// </summary>
	private static async Task<bool> MatriculaRepetidaAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? ignorarSqOrg,
		VinculoOrgaoDados dados,
		CancellationToken cancellationToken)
	{
		var matricula = Texto(dados.Matricula);

		if (matricula is null)
		{
			return false;
		}

		var orgao = dados.CdOrg!.Value;

		return await db.VinculosOrgao.AnyAsync(
			v => v.CD_EMP == emp
				&& v.SQ_PES == sqPesCli
				&& v.CD_ORG == orgao
				&& v.MT_PES == matricula
				&& (ignorarSqOrg == null || v.SQ_ORG != ignorarSqOrg),
			cancellationToken);
	}

	private static void Aplicar(VinculoOrgaoDados d, T000RPOR v)
	{
		v.CD_ORG = d.CdOrg!.Value;
		v.CD_LOT = d.CdLotacao;
		v.CD_SIT_ORG = d.CdSituacao;
		v.CD_CG = d.CdCargo;
		v.MT_PES = Texto(d.Matricula);
		v.DV_MT = Texto(d.DigitoMatricula);
		v.MT_INS = Texto(d.MatriculaInstituidor);
		v.VR_SAL = d.Salario;
		v.VR_SAL_LIQ = d.SalarioLiquido;
	}

	private static IQueryable<VinculoOrgaoDto> Projetar(SigeraDbContext db, decimal emp, decimal sqPesCli)
	{
		return db.VinculosOrgao
			.AsNoTracking()
			.Where(v => v.CD_EMP == emp && v.SQ_PES == sqPesCli)
			.OrderBy(v => v.SQ_ORG)
			.Select(v => new VinculoOrgaoDto
			{
				SqOrg = v.SQ_ORG,
				CdOrg = v.CD_ORG,
				NomeOrgao = db.Orgaos.Where(o => o.CD_EMP == v.CD_EMP && o.CD_ORG == v.CD_ORG).Select(o => o.NM_ORG).FirstOrDefault(),
				SiglaOrgao = db.Orgaos.Where(o => o.CD_EMP == v.CD_EMP && o.CD_ORG == v.CD_ORG).Select(o => o.SGL_ORG).FirstOrDefault(),
				CdLotacao = v.CD_LOT,
				Lotacao = db.Lotacoes.Where(l => l.CD_EMP == v.CD_EMP && l.CD_LOT == v.CD_LOT).Select(l => l.DE_LOT).FirstOrDefault(),
				CdSituacao = v.CD_SIT_ORG,
				Situacao = db.SituacoesVinculo.Where(s => s.CD_EMP == v.CD_EMP && s.CD_SIT_ORG == v.CD_SIT_ORG).Select(s => s.DE_SIT_ORG).FirstOrDefault(),
				CdCargo = v.CD_CG,
				Cargo = db.Cargos.Where(c => c.CD_EMP == v.CD_EMP && c.CD_CG == v.CD_CG).Select(c => c.DE_CG).FirstOrDefault(),
				Matricula = v.MT_PES,
				DigitoMatricula = v.DV_MT,
				MatriculaInstituidor = v.MT_INS,
				Salario = v.VR_SAL,
				SalarioLiquido = v.VR_SAL_LIQ
			});
	}

	private static async Task<VinculoOrgaoDto?> ObterAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal sqOrg,
		CancellationToken cancellationToken)
	{
		return await Projetar(db, emp, sqPesCli).FirstOrDefaultAsync(v => v.SqOrg == sqOrg, cancellationToken);
	}
}
