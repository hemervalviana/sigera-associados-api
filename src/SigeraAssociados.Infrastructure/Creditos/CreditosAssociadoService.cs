using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Creditos;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Creditos;
using SigeraAssociados.Infrastructure.Associados;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Creditos;

/// <summary>
/// Créditos do associado (T009EXTRA). SQ_PGT = MAX + 1 do associado, com
/// UPDLOCK, HOLDLOCK. Saldo (VR_SLD) calculado pela forma de pagamento,
/// e exclusão só quando valor = saldo, como no legado (RegrasCredito).
/// </summary>
public sealed class CreditosAssociadoService : ICreditosAssociadoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public CreditosAssociadoService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<CreditoDto>?> ListarAsync(
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		var creditos = await db.Creditos
			.AsNoTracking()
			.Where(c => c.CD_EMP == emp && c.SQ_PES_CLI == sqPesCli)
			.OrderByDescending(c => c.SQ_PGT)
			.ToListAsync(cancellationToken);

		var descricoes = await Descricoes.CarregarAsync(db, emp, cancellationToken);

		return creditos.Select(c => ParaDto(c, descricoes)).ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CreditoDto>> IncluirAsync(
		decimal sqPesCli,
		CreditoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (RegrasCredito.Validar(dados) is string erro)
		{
			return Invalido<CreditoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<CreditoDto>("Associado não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, emp, sqPesCli, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<CreditoDto>(erroReferencia);
		}

		var forma = dados.CdFormaPagamento!.Value;
		var idGeraSaldo = await GeraSaldoAsync(db, emp, forma, cancellationToken);

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqCredito = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_PGT), 0) + 1 AS [Value] FROM dbo.T009EXTRA WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND SQ_PES_CLI = {sqPesCli}",
			cancellationToken);

		if (sqCredito > RegrasCredito.SequenciaMaxima)
		{
			return Invalido<CreditoDto>("O associado atingiu o limite de sequência de créditos.");
		}

		var valor = dados.Valor ?? 0;

		var credito = new T009EXTRA
		{
			CD_EMP = emp,
			SQ_PES_CLI = sqPesCli,
			SQ_PGT = sqCredito,
			DT_PGT = (dados.DataCredito ?? DateTime.Today).Date,
			VR_PGT = valor,
			ID_PGT_AUT = dados.TipoBaixa ?? RegrasCredito.BaixaCredito,
			VR_SLD = RegrasCredito.CalcularSaldo(valor, forma, idGeraSaldo)
		};

		AplicarComplementos(dados, credito);
		db.Creditos.Add(credito);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<CreditoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Crédito incluído.", ParaDto(credito, await Descricoes.CarregarAsync(db, emp, cancellationToken)));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CreditoDto>> AlterarAsync(
		decimal sqPesCli,
		decimal sqCredito,
		CreditoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (RegrasCredito.Validar(dados) is string erro)
		{
			return Invalido<CreditoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<CreditoDto>("Associado não encontrado.");
		}

		var credito = await BuscarAsync(db, emp, sqPesCli, sqCredito, cancellationToken);

		if (credito is null)
		{
			return NaoEncontrado<CreditoDto>("Crédito não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, emp, sqPesCli, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<CreditoDto>(erroReferencia);
		}

		// Data, valor e tipo de baixa omitidos mantêm o que está gravado.
		var valor = dados.Valor ?? credito.VR_PGT ?? 0;
		var forma = dados.CdFormaPagamento!.Value;

		// Como o itemchanged do legado: o saldo só é recalculado quando o
		// valor ou a forma de pagamento mudam.
		if (valor != (credito.VR_PGT ?? 0) || forma != credito.TP_PGT)
		{
			credito.VR_SLD = RegrasCredito.CalcularSaldo(valor, forma, await GeraSaldoAsync(db, emp, forma, cancellationToken));
		}

		credito.VR_PGT = valor;

		if (dados.DataCredito is DateTime data)
		{
			credito.DT_PGT = data.Date;
		}

		if (dados.TipoBaixa is decimal tipoBaixa)
		{
			credito.ID_PGT_AUT = tipoBaixa;
		}

		AplicarComplementos(dados, credito);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<CreditoDto>(erroGravacao);
		}

		return Sucesso("Crédito alterado.", ParaDto(credito, await Descricoes.CarregarAsync(db, emp, cancellationToken)));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CreditoDto>> ExcluirAsync(
		decimal sqPesCli,
		decimal sqCredito,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirAssociadoAsync(db, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<CreditoDto>("Associado não encontrado.");
		}

		var credito = await BuscarAsync(db, emp, sqPesCli, sqCredito, cancellationToken);

		if (credito is null)
		{
			return NaoEncontrado<CreditoDto>("Crédito não encontrado.");
		}

		// Regra do legado (oe_delete): valor do pagamento diferente do saldo não exclui.
		if (!RegrasCredito.PodeExcluir(credito.VR_PGT, credito.VR_SLD))
		{
			return Conflito<CreditoDto>("Valor do pagamento diferente do valor do saldo: o crédito não pode ser excluído.");
		}

		var dto = ParaDto(credito, await Descricoes.CarregarAsync(db, emp, cancellationToken));
		db.Creditos.Remove(credito);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<CreditoDto>(erroGravacao)
			: Sucesso("Crédito excluído.", dto);
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

	private static Task<T009EXTRA?> BuscarAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal sqCredito,
		CancellationToken cancellationToken) =>
		db.Creditos.FirstOrDefaultAsync(
			c => c.CD_EMP == emp && c.SQ_PES_CLI == sqPesCli && c.SQ_PGT == sqCredito,
			cancellationToken);

	/// <summary>
	/// ID_GER_SLD da forma de pagamento (forma 0 não é consultada).
	/// </summary>
	private static async Task<decimal?> GeraSaldoAsync(
		SigeraDbContext db,
		decimal emp,
		decimal forma,
		CancellationToken cancellationToken)
	{
		if (forma <= 0)
		{
			return null;
		}

		return await db.FormasPagamento
			.AsNoTracking()
			.Where(f => f.CD_EMP == emp && f.TP_FMA_PGT == forma)
			.Select(f => f.ID_GER_SLD)
			.FirstOrDefaultAsync(cancellationToken);
	}

	/// <summary>
	/// Confere forma de pagamento, filial, contrato do associado, convênio,
	/// averbação (T009RBSR) e produto.
	/// </summary>
	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		CreditoDados dados,
		CancellationToken cancellationToken)
	{
		var forma = dados.CdFormaPagamento!.Value;

		if (forma > 0 && !await db.FormasPagamento.AnyAsync(f => f.CD_EMP == emp && f.TP_FMA_PGT == forma, cancellationToken))
		{
			return $"A forma de pagamento {forma:0} não está cadastrada (T009FRPG).";
		}

		if (dados.CdFil is decimal filial
			&& !await db.Filiais.AnyAsync(f => f.CD_EMP == emp && f.CD_FIL == filial, cancellationToken))
		{
			return $"A filial {filial:0} não está cadastrada (T000PARA).";
		}

		if (dados.SqContrato is decimal contrato)
		{
			var fil = dados.CdFil!.Value;

			if (!await db.Contratos.AnyAsync(
					c => c.CD_EMP == emp && c.CD_FIL == fil && c.SQ_CTR == contrato && c.SQ_PES_CLI == sqPesCli,
					cancellationToken))
			{
				return $"O contrato {contrato:0} da filial {fil:0} não existe ou não é deste associado.";
			}
		}

		if (dados.CdConvenio is decimal convenio
			&& !await db.Convenios.AnyAsync(c => c.CD_EMP == emp && c.CD_CVN == convenio, cancellationToken))
		{
			return $"O convênio {convenio:0} não está cadastrado (T009CONV).";
		}

		if (dados.CdAverbacao is decimal averbacao)
		{
			var fil = dados.CdFil!.Value;
			var cvn = dados.CdConvenio!.Value;

			var existe = await NumeroAsync(db,
				$"SELECT CAST(COUNT(*) AS decimal(10, 0)) AS [Value] FROM dbo.T009RBSR WHERE CD_EMP = {emp} AND CD_FIL = {fil} AND CD_CVN = {cvn} AND CD_AVB = {averbacao}",
				cancellationToken);

			if (existe == 0)
			{
				return $"A averbação {averbacao:0} não existe no convênio {cvn:0} da filial {fil:0} (T009RBSR).";
			}
		}

		if (dados.CdProduto is decimal produto
			&& !await db.Produtos.AnyAsync(p => p.CD_EMP == emp && p.CD_PRD == produto, cancellationToken))
		{
			return $"O produto {produto:0} não está cadastrado (T009PRDT).";
		}

		return null;
	}

	/// <summary>
	/// Campos que não entram no cálculo do saldo.
	/// </summary>
	private static void AplicarComplementos(CreditoDados d, T009EXTRA c)
	{
		c.TP_PGT = d.CdFormaPagamento!.Value;
		c.NR_CHQ = d.NumeroCheque;
		c.DE_OBS = Texto(d.Observacao);
		c.CD_FIL = d.CdFil;
		c.SQ_CTR = d.SqContrato;
		c.NR_PCL = d.NumeroParcela;
		c.CD_CVN = d.CdConvenio;
		c.CD_AVB = d.CdAverbacao;
		c.MT_PES = Texto(d.Matricula);
		c.DV_MT = Texto(d.DigitoMatricula);
		c.CD_PRD = d.CdProduto;
	}

	private static CreditoDto ParaDto(T009EXTRA c, Descricoes descricoes) => new()
	{
		SqCredito = c.SQ_PGT,
		DataCredito = c.DT_PGT,
		Valor = c.VR_PGT ?? 0,
		Saldo = c.VR_SLD,
		CdFormaPagamento = c.TP_PGT,
		FormaPagamento = descricoes.Formas.GetValueOrDefault(c.TP_PGT),
		TipoBaixa = c.ID_PGT_AUT,
		DescricaoTipoBaixa = RegrasCredito.DescricaoTipoBaixa(c.ID_PGT_AUT),
		NumeroCheque = c.NR_CHQ,
		Observacao = c.DE_OBS,
		CdFil = c.CD_FIL,
		SqContrato = c.SQ_CTR,
		NumeroParcela = c.NR_PCL,
		CdConvenio = c.CD_CVN,
		Convenio = c.CD_CVN is decimal cvn ? descricoes.Convenios.GetValueOrDefault(cvn) : null,
		CdAverbacao = c.CD_AVB,
		Matricula = c.MT_PES,
		DigitoMatricula = c.DV_MT,
		CdProduto = c.CD_PRD,
		Produto = c.CD_PRD is decimal prd ? descricoes.Produtos.GetValueOrDefault(prd) : null,
		PodeExcluir = RegrasCredito.PodeExcluir(c.VR_PGT, c.VR_SLD)
	};

	/// <summary>
	/// Descrições das tabelas pequenas (formas, convênios e produtos) da empresa.
	/// </summary>
	private sealed record Descricoes(
		IReadOnlyDictionary<decimal, string?> Formas,
		IReadOnlyDictionary<decimal, string?> Convenios,
		IReadOnlyDictionary<decimal, string?> Produtos)
	{
		public static async Task<Descricoes> CarregarAsync(SigeraDbContext db, decimal emp, CancellationToken cancellationToken) =>
			new(
				await db.FormasPagamento.AsNoTracking().Where(f => f.CD_EMP == emp)
					.ToDictionaryAsync(f => f.TP_FMA_PGT, f => f.DE_FMA_PGT, cancellationToken),
				await db.Convenios.AsNoTracking().Where(c => c.CD_EMP == emp)
					.ToDictionaryAsync(c => c.CD_CVN, c => (string?)c.NM_CVN, cancellationToken),
				await db.Produtos.AsNoTracking().Where(p => p.CD_EMP == emp)
					.ToDictionaryAsync(p => p.CD_PRD, p => (string?)p.DE_PRD, cancellationToken));
	}
}
