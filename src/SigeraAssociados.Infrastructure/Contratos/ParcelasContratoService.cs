using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Contratos;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Contratos;

/// <summary>
/// Parcelas do contrato (T009PAGA), com as regras de o_tc_t009paga:
/// NR_PCL = MAX + 1 do contrato (UPDLOCK, HOLDLOCK); baixa manual pelo
/// valor pago; parâmetro 1 da filial exige saldo zero na parcela paga;
/// contrato quitado (9) trava as parcelas. Toda gravação acerta QT_PCL e,
/// no produto 2, DT_ULT_DCT do contrato.
/// </summary>
public sealed class ParcelasContratoService : IParcelasContratoService
{
	private const decimal QuantidadeMaxima = 9_999;

	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public ParcelasContratoService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<ResumoParcelas?> ListarAsync(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var contrato = await AbrirAsync(db, cdFil, sqCtr, rastrear: false, cancellationToken);

		return contrato is null ? null : await CarregarAsync(db, contrato, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ParcelaDto>> IncluirAsync(
		decimal cdFil,
		decimal sqCtr,
		ParcelaDados dados,
		string login,
		CancellationToken cancellationToken = default)
	{
		if (RegrasContrato.ValidarParcela(dados) is string erro)
		{
			return Invalido<ParcelaDto>(erro);
		}

		await using var db = CriarContexto();

		var contrato = await AbrirAsync(db, cdFil, sqCtr, rastrear: true, cancellationToken);

		if (contrato is null)
		{
			return NaoEncontrado<ParcelaDto>("Contrato não encontrado.");
		}

		if (RegrasContrato.ParcelasTravadas(contrato.CD_ST_CTR))
		{
			return Conflito<ParcelaDto>(ContratoQuitado);
		}

		var valor = dados.Valor!.Value;
		var (pago, saldo, tipoBaixa, forma) = RegrasContrato.Pagamento(valor, dados.ValorPago);

		if (RegrasContrato.ValidarSaldoZero(await SaldoZeroAsync(db, contrato, cancellationToken), pago, saldo) is string erroSaldo)
		{
			return Invalido<ParcelaDto>(erroSaldo);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var emp = contrato.CD_EMP;

		var numero = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(NR_PCL), 0) + 1 AS [Value] FROM dbo.T009PAGA WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND CD_FIL = {cdFil} AND SQ_CTR = {sqCtr}",
			cancellationToken);

		if (numero > RegrasContrato.NumeroParcelaMaximo
			|| await Parcelas(db, contrato).CountAsync(cancellationToken) >= QuantidadeMaxima)
		{
			return Invalido<ParcelaDto>("O contrato atingiu o limite de parcelas.");
		}

		var parcela = new T009PAGA
		{
			CD_EMP = emp,
			CD_FIL = cdFil,
			SQ_CTR = sqCtr,
			NR_PCL = numero,
			DT_VEN = dados.DataVencimento!.Value.Date,
			VR_PCL = valor,
			DT_PGT = pago > 0 ? dados.DataPagamento!.Value.Date : null,
			VR_PGT = pago,
			VR_SD_PCL = saldo,
			ID_PGT_AUT = tipoBaixa,
			TP_PGT = forma,
			DT_GER_PCL = DateTime.Now,
			NM_USU = login.Length <= 50 ? login : login[..50]
		};

		db.Parcelas.Add(parcela);

		if (await GravarAsync(db, contrato, cancellationToken) is string erroGravacao)
		{
			return Invalido<ParcelaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso($"Parcela {numero:0} incluída; o contrato tem agora {contrato.QT_PCL:0} parcela(s).", ParaDto(parcela));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ParcelaDto>> AlterarAsync(
		decimal cdFil,
		decimal sqCtr,
		decimal numeroParcela,
		ParcelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (RegrasContrato.ValidarParcela(dados) is string erro)
		{
			return Invalido<ParcelaDto>(erro);
		}

		await using var db = CriarContexto();

		var contrato = await AbrirAsync(db, cdFil, sqCtr, rastrear: true, cancellationToken);

		if (contrato is null)
		{
			return NaoEncontrado<ParcelaDto>("Contrato não encontrado.");
		}

		if (RegrasContrato.ParcelasTravadas(contrato.CD_ST_CTR))
		{
			return Conflito<ParcelaDto>(ContratoQuitado);
		}

		var parcela = await Parcelas(db, contrato).FirstOrDefaultAsync(p => p.NR_PCL == numeroParcela, cancellationToken);

		if (parcela is null)
		{
			return NaoEncontrado<ParcelaDto>("Parcela não encontrada.");
		}

		parcela.DT_VEN = dados.DataVencimento!.Value.Date;
		parcela.VR_PCL = dados.Valor!.Value;

		// Como o itemchanged do legado: a baixa (tipo e forma) só muda quando o
		// valor pago muda; mudar só o valor da parcela recalcula o saldo e
		// preserva uma baixa automática.
		var pagoNovo = dados.ValorPago is > 0 ? dados.ValorPago.Value : 0;

		if (pagoNovo != (parcela.VR_PGT ?? 0))
		{
			var (pago, saldo, tipoBaixa, forma) = RegrasContrato.Pagamento(parcela.VR_PCL, dados.ValorPago);
			parcela.VR_PGT = pago;
			parcela.VR_SD_PCL = saldo;
			parcela.ID_PGT_AUT = tipoBaixa;
			parcela.TP_PGT = forma;
		}
		else
		{
			parcela.VR_SD_PCL = (parcela.VR_PGT ?? 0) - parcela.VR_PCL;
		}

		parcela.DT_PGT = pagoNovo > 0 ? dados.DataPagamento!.Value.Date : null;

		if (RegrasContrato.ValidarSaldoZero(await SaldoZeroAsync(db, contrato, cancellationToken), parcela.VR_PGT, parcela.VR_SD_PCL) is string erroSaldo)
		{
			return Invalido<ParcelaDto>(erroSaldo);
		}

		return await GravarAsync(db, contrato, cancellationToken) is string erroGravacao
			? Invalido<ParcelaDto>(erroGravacao)
			: Sucesso("Parcela alterada.", ParaDto(parcela));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ParcelaDto>> ExcluirAsync(
		decimal cdFil,
		decimal sqCtr,
		decimal numeroParcela,
		bool podeExcluirPaga,
		bool podeExcluirNaoPaga,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var contrato = await AbrirAsync(db, cdFil, sqCtr, rastrear: true, cancellationToken);

		if (contrato is null)
		{
			return NaoEncontrado<ParcelaDto>("Contrato não encontrado.");
		}

		if (RegrasContrato.ParcelasTravadas(contrato.CD_ST_CTR))
		{
			return Conflito<ParcelaDto>(ContratoQuitado);
		}

		var parcela = await Parcelas(db, contrato).FirstOrDefaultAsync(p => p.NR_PCL == numeroParcela, cancellationToken);

		if (parcela is null)
		{
			return NaoEncontrado<ParcelaDto>("Parcela não encontrada.");
		}

		// oe_delete: parcela paga só o Financeiro apaga (aqui também os administradores).
		if (RegrasContrato.ParcelaPaga(parcela.ID_PGT_AUT) ? !podeExcluirPaga : !podeExcluirNaoPaga)
		{
			return Proibido<ParcelaDto>(RegrasContrato.ParcelaPaga(parcela.ID_PGT_AUT)
				? "Somente Administradores e o grupo Financeiro podem excluir parcela paga."
				: "Somente Administradores e Gestores podem excluir parcela não paga.");
		}

		// O contrato não fica sem parcelas (o legado não salva sem parcelas).
		if (await Parcelas(db, contrato).CountAsync(cancellationToken) <= 1)
		{
			return Conflito<ParcelaDto>("O contrato precisa ter ao menos uma parcela.");
		}

		var dto = ParaDto(parcela);
		db.Parcelas.Remove(parcela);

		return await GravarAsync(db, contrato, cancellationToken) is string erroGravacao
			? Conflito<ParcelaDto>(erroGravacao)
			: Sucesso("Parcela excluída.", dto);
	}

	private const string ContratoQuitado = "Contrato quitado (situação 9): as parcelas não podem ser incluídas, alteradas nem excluídas.";

	/// <summary>f_retornar_parametro(1): parâmetro 1 da filial do contrato ligado (TP_FLG = 'S').</summary>
	private static Task<bool> SaldoZeroAsync(SigeraDbContext db, T009CTRT c, CancellationToken cancellationToken) =>
		db.Parametros.AsNoTracking().AnyAsync(
			p => p.CD_EMP == c.CD_EMP && p.CD_FIL == c.CD_FIL && p.SQ_PRM == RegrasContrato.ParametroSaldoZero && p.TP_FLG == "S",
			cancellationToken);

	// ----- Apoio -----

	/// <summary>Parcelas e totais do contrato (usado também no detalhe do contrato).</summary>
	internal static async Task<ResumoParcelas> CarregarAsync(SigeraDbContext db, T009CTRT contrato, CancellationToken cancellationToken)
	{
		var itens = (await Parcelas(db, contrato).AsNoTracking()
				.OrderBy(p => p.NR_PCL)
				.ToListAsync(cancellationToken))
			.Select(ParaDto)
			.ToList();

		return new ResumoParcelas
		{
			Quantidade = itens.Count,
			Pagas = itens.Count(p => p.Paga),
			TotalParcelas = itens.Sum(p => p.Valor),
			TotalPago = itens.Sum(p => p.ValorPago ?? 0),
			Itens = itens
		};
	}

	private static IQueryable<T009PAGA> Parcelas(SigeraDbContext db, T009CTRT c) =>
		db.Parcelas.Where(p => p.CD_EMP == c.CD_EMP && p.CD_FIL == c.CD_FIL && p.SQ_CTR == c.SQ_CTR);

	/// <summary>Grava a parcela e depois acerta QT_PCL/DT_ULT_DCT do contrato.</summary>
	private static async Task<string?> GravarAsync(SigeraDbContext db, T009CTRT contrato, CancellationToken cancellationToken)
	{
		// As duas gravações andam juntas: parcela sem QT_PCL acertado não fica.
		var propria = db.Database.CurrentTransaction is null
			? await db.Database.BeginTransactionAsync(cancellationToken)
			: null;

		try
		{
			if (await SalvarAsync(db, cancellationToken) is string erro)
			{
				return erro;
			}

			await AcessoContrato.SincronizarParcelasAsync(db, contrato, cancellationToken);

			if (await SalvarAsync(db, cancellationToken) is string erroContrato)
			{
				return erroContrato;
			}

			if (propria is not null)
			{
				await propria.CommitAsync(cancellationToken);
			}

			return null;
		}
		finally
		{
			if (propria is not null)
			{
				await propria.DisposeAsync();
			}
		}
	}

	private async Task<T009CTRT?> AbrirAsync(SigeraDbContext db, decimal cdFil, decimal sqCtr, bool rastrear, CancellationToken cancellationToken)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		return await ObterEmpresaAsync(db, cancellationToken) is decimal emp
			? await AcessoContrato.CarregarAsync(db, emp, cdFil, sqCtr, escopo, rastrear, cancellationToken)
			: null;
	}

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	private static ParcelaDto ParaDto(T009PAGA p) => new()
	{
		NumeroParcela = p.NR_PCL,
		DataVencimento = p.DT_VEN,
		Valor = p.VR_PCL,
		DataPagamento = p.DT_PGT,
		ValorPago = p.VR_PGT,
		ValorMulta = p.VR_MLT,
		ValorJuros = p.VR_JUR,
		ValorDesconto = p.VR_DSC,
		Saldo = p.VR_SD_PCL,
		NumeroCheque = p.NR_CHQ,
		Remessa = p.ID_REM,
		TipoBaixa = p.ID_PGT_AUT,
		DescricaoTipoBaixa = RegrasContrato.DescricaoTipoBaixa(p.ID_PGT_AUT),
		CdFormaPagamento = p.TP_PGT,
		CdConvenio = p.CD_CVN,
		CdAverbacao = p.CD_AVB,
		IdCobranca = p.ID_COB_PCL,
		DataGeracao = p.DT_GER_PCL,
		Usuario = p.NM_USU,
		Paga = RegrasContrato.ParcelaPaga(p.ID_PGT_AUT)
	};
}
