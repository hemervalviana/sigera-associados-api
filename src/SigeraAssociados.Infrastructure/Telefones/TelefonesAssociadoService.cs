using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Telefones;
using SigeraAssociados.Application.Telefones;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Telefones;

/// <summary>
/// Telefones do associado (T000TELE). SQ_TELE = MAX + 1 da empresa
/// (numeric(18,0), única na tabela), com UPDLOCK, HOLDLOCK.
/// </summary>
public sealed class TelefonesAssociadoService : ITelefonesAssociadoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public TelefonesAssociadoService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<TelefoneAssociadoDto>?> ListarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return (await Projetar(db, emp, sqPesCli).ToListAsync(cancellationToken))
			.Select(ParaDto)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<TelefoneAssociadoDto>> IncluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		TelefoneAssociadoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTelefoneAssociado.Validar(dados) is string erro)
		{
			return Invalido<TelefoneAssociadoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<TelefoneAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		if (await ValidarTipoAsync(db, emp, dados, cancellationToken) is string erroTipo)
		{
			return Invalido<TelefoneAssociadoDto>(erroTipo);
		}

		if (await DuplicadoAsync(db, emp, sqPesCli, null, dados, cancellationToken))
		{
			return Conflito<TelefoneAssociadoDto>("O associado já tem este telefone.");
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqTelefone = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_TELE), 0) + 1 AS [Value] FROM dbo.T000TELE WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		// O default da coluna é -1: começa em 1 mesmo que existam negativos.
		if (sqTelefone < 1)
		{
			sqTelefone = 1;
		}

		var telefone = new T000TELE { CD_EMP = emp, SQ_PES = sqPesCli, SQ_TELE = sqTelefone };
		Aplicar(dados, telefone);
		db.Telefones.Add(telefone);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<TelefoneAssociadoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Telefone incluído.", await ObterAsync(db, emp, sqPesCli, sqTelefone, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<TelefoneAssociadoDto>> AlterarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqTelefone,
		TelefoneAssociadoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTelefoneAssociado.Validar(dados) is string erro)
		{
			return Invalido<TelefoneAssociadoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<TelefoneAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var telefone = await db.Telefones.FirstOrDefaultAsync(
			t => t.CD_EMP == emp && t.SQ_PES == sqPesCli && t.SQ_TELE == sqTelefone,
			cancellationToken);

		if (telefone is null)
		{
			return NaoEncontrado<TelefoneAssociadoDto>("Telefone não encontrado.");
		}

		if (await ValidarTipoAsync(db, emp, dados, cancellationToken) is string erroTipo)
		{
			return Invalido<TelefoneAssociadoDto>(erroTipo);
		}

		if (await DuplicadoAsync(db, emp, sqPesCli, sqTelefone, dados, cancellationToken))
		{
			return Conflito<TelefoneAssociadoDto>("O associado já tem este telefone.");
		}

		Aplicar(dados, telefone);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<TelefoneAssociadoDto>(erroGravacao);
		}

		return Sucesso("Telefone alterado.", await ObterAsync(db, emp, sqPesCli, sqTelefone, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<TelefoneAssociadoDto>> ExcluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqTelefone,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<TelefoneAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var dto = await ObterAsync(db, emp, sqPesCli, sqTelefone, cancellationToken);

		var telefone = await db.Telefones.FirstOrDefaultAsync(
			t => t.CD_EMP == emp && t.SQ_PES == sqPesCli && t.SQ_TELE == sqTelefone,
			cancellationToken);

		if (telefone is null)
		{
			return NaoEncontrado<TelefoneAssociadoDto>("Telefone não encontrado.");
		}

		db.Telefones.Remove(telefone);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<TelefoneAssociadoDto>(erroGravacao)
			: Sucesso("Telefone excluído.", dto!);
	}

	// ----- Apoio -----

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	/// <summary>
	/// Empresa da base quando o titular (associado no escopo do usuário, ou
	/// corretor não excluído) existe; null para tratar como não encontrado.
	/// </summary>
	private Task<decimal?> AbrirTitularAsync(
		SigeraDbContext db,
		TitularCadastro titular,
		decimal sqPes,
		CancellationToken cancellationToken) =>
		AcessoTitular.AbrirAsync(db, _escopoAcesso, titular, sqPes, cancellationToken);

	private static async Task<string?> ValidarTipoAsync(
		SigeraDbContext db,
		decimal emp,
		TelefoneAssociadoDados dados,
		CancellationToken cancellationToken)
	{
		if (dados.CdTipo is not decimal tipo)
		{
			return null;
		}

		return await db.TiposTelefone.AnyAsync(t => t.CD_EMP == emp && t.CD_TP_FN == tipo, cancellationToken)
			? null
			: $"O tipo de telefone {tipo:0} não está cadastrado (T000TTEL).";
	}

	/// <summary>
	/// Mesmo DDD e número em outro telefone do associado. Compara só os
	/// dígitos, porque o legado pode ter gravado o número com traço.
	/// </summary>
	private static async Task<bool> DuplicadoAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? sqTelefone,
		TelefoneAssociadoDados dados,
		CancellationToken cancellationToken)
	{
		var ddd = dados.Ddd!.Value;
		var numero = ValidacaoTelefoneAssociado.Normalizar(dados.Numero!);

		var numeros = await db.Telefones
			.AsNoTracking()
			.Where(t => t.CD_EMP == emp && t.SQ_PES == sqPesCli && t.NR_DDD == ddd)
			.Where(t => sqTelefone == null || t.SQ_TELE != sqTelefone)
			.Select(t => t.NR_FN)
			.ToListAsync(cancellationToken);

		return numeros.Any(n => ValidacaoTelefoneAssociado.Normalizar(n) == numero);
	}

	private static void Aplicar(TelefoneAssociadoDados d, T000TELE t)
	{
		t.NR_DDD = d.Ddd!.Value;
		t.NR_FN = ValidacaoTelefoneAssociado.Normalizar(d.Numero!);
		t.CD_TP_FN = d.CdTipo;
	}

	private sealed record LinhaTelefone(
		decimal SqTelefone,
		decimal Ddd,
		string Numero,
		decimal? CdTipo,
		string? Tipo);

	/// <summary>
	/// Telefones da pessoa (ou só um, com sqTelefone). O filtro vem antes do Select.
	/// </summary>
	private static IQueryable<LinhaTelefone> Projetar(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? sqTelefone = null)
	{
		var telefones = db.Telefones
			.AsNoTracking()
			.Where(t => t.CD_EMP == emp && t.SQ_PES == sqPesCli);

		if (sqTelefone is decimal sequencia)
		{
			telefones = telefones.Where(t => t.SQ_TELE == sequencia);
		}

		return telefones
			.OrderBy(t => t.SQ_TELE)
			.Select(t => new LinhaTelefone(
				t.SQ_TELE,
				t.NR_DDD,
				t.NR_FN,
				t.CD_TP_FN,
				db.TiposTelefone.Where(p => p.CD_EMP == t.CD_EMP && p.CD_TP_FN == t.CD_TP_FN).Select(p => p.DE_TP_FN).FirstOrDefault()));
	}

	private static TelefoneAssociadoDto ParaDto(LinhaTelefone l) => new()
	{
		SqTelefone = l.SqTelefone,
		Ddd = l.Ddd,
		Numero = l.Numero.Trim(),
		Formatado = ValidacaoTelefoneAssociado.Formatar(l.Ddd, l.Numero),
		CdTipo = l.CdTipo,
		Tipo = l.Tipo
	};

	private static async Task<TelefoneAssociadoDto?> ObterAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal sqTelefone,
		CancellationToken cancellationToken)
	{
		var linha = await Projetar(db, emp, sqPesCli, sqTelefone).FirstOrDefaultAsync(cancellationToken);

		return linha is null ? null : ParaDto(linha);
	}
}
