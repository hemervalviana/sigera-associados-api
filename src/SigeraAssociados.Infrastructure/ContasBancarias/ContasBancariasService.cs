using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.ContasBancarias;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.ContasBancarias;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.ContasBancarias;

/// <summary>
/// Contas bancárias do associado (T000CCOR). SQ_CTA_COR = MAX + 1 da
/// pessoa, com UPDLOCK, HOLDLOCK.
/// </summary>
public sealed class ContasBancariasService : IContasBancariasService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public ContasBancariasService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ContaBancariaDto>?> ListarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return await BuscarAsync(db, emp, sqPesCli, null, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ContaBancariaDto>> IncluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		ContaBancariaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoContaBancaria.Validar(dados) is string erro)
		{
			return Invalido<ContaBancariaDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ContaBancariaDto>(AcessoTitular.NaoEncontrado(titular));
		}

		if (await ValidarBancoAsync(db, emp, dados, cancellationToken) is string erroBanco)
		{
			return Invalido<ContaBancariaDto>(erroBanco);
		}

		if (await ContaRepetidaAsync(db, emp, sqPesCli, null, dados, cancellationToken))
		{
			return Conflito<ContaBancariaDto>("O associado já tem esta conta (mesmo banco, agência e número).");
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqConta = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_CTA_COR), 0) + 1 AS [Value] FROM dbo.T000CCOR WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND SQ_PES = {sqPesCli}",
			cancellationToken);

		if (sqConta > ValidacaoContaBancaria.MaximoContas)
		{
			return Invalido<ContaBancariaDto>("O associado atingiu o limite de 99 contas.");
		}

		var conta = new T000CCOR { CD_EMP = emp, SQ_PES = sqPesCli, SQ_CTA_COR = sqConta };
		Aplicar(dados, conta);

		// Conta nova sem situação informada: ATIVA.
		conta.ID_CC_ATV ??= RegrasContaBancaria.Ativa;
		db.ContasBancarias.Add(conta);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ContaBancariaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Conta incluída.", await ObterAsync(db, emp, sqPesCli, sqConta, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ContaBancariaDto>> AlterarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqConta,
		ContaBancariaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoContaBancaria.Validar(dados) is string erro)
		{
			return Invalido<ContaBancariaDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ContaBancariaDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var conta = await db.ContasBancarias.FirstOrDefaultAsync(
			c => c.CD_EMP == emp && c.SQ_PES == sqPesCli && c.SQ_CTA_COR == sqConta,
			cancellationToken);

		if (conta is null)
		{
			return NaoEncontrado<ContaBancariaDto>("Conta não encontrada.");
		}

		if (await ValidarBancoAsync(db, emp, dados, cancellationToken) is string erroBanco)
		{
			return Invalido<ContaBancariaDto>(erroBanco);
		}

		if (await ContaRepetidaAsync(db, emp, sqPesCli, sqConta, dados, cancellationToken))
		{
			return Conflito<ContaBancariaDto>("O associado já tem outra conta com o mesmo banco, agência e número.");
		}

		Aplicar(dados, conta);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<ContaBancariaDto>(erroGravacao)
			: Sucesso("Conta alterada.", await ObterAsync(db, emp, sqPesCli, sqConta, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ContaBancariaDto>> ExcluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqConta,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ContaBancariaDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var dto = await ObterAsync(db, emp, sqPesCli, sqConta, cancellationToken);

		var conta = await db.ContasBancarias.FirstOrDefaultAsync(
			c => c.CD_EMP == emp && c.SQ_PES == sqPesCli && c.SQ_CTA_COR == sqConta,
			cancellationToken);

		if (conta is null)
		{
			return NaoEncontrado<ContaBancariaDto>("Conta não encontrada.");
		}

		db.ContasBancarias.Remove(conta);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ContaBancariaDto>(erroGravacao)
			: Sucesso("Conta excluída.", dto);
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

	private static async Task<string?> ValidarBancoAsync(
		SigeraDbContext db,
		decimal emp,
		ContaBancariaDados dados,
		CancellationToken cancellationToken)
	{
		var banco = dados.CdBanco!.Value;

		return await db.Bancos.AnyAsync(b => b.CD_EMP == emp && b.CD_BCO == banco, cancellationToken)
			? null
			: $"O banco {banco:0} não está cadastrado (T000BANC).";
	}

	private static Task<bool> ContaRepetidaAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? ignorarSqConta,
		ContaBancariaDados dados,
		CancellationToken cancellationToken)
	{
		var banco = dados.CdBanco!.Value;
		var agencia = dados.Agencia!.Value;
		var numero = dados.NumeroConta!.Value;

		return db.ContasBancarias.AnyAsync(
			c => c.CD_EMP == emp
				&& c.SQ_PES == sqPesCli
				&& c.CD_BCO == banco
				&& c.CD_AG == agencia
				&& c.NR_CTA_COR == numero
				&& (ignorarSqConta == null || c.SQ_CTA_COR != ignorarSqConta),
			cancellationToken);
	}

	private static void Aplicar(ContaBancariaDados d, T000CCOR c)
	{
		c.CD_BCO = d.CdBanco!.Value;
		c.DV_BCO = Texto(d.DigitoBanco)?.ToUpperInvariant();
		c.CD_AG = d.Agencia!.Value;
		c.DV_AG = Texto(d.DigitoAgencia)?.ToUpperInvariant();
		c.NR_CTA_COR = d.NumeroConta!.Value;
		c.DV_CTA_COR = Texto(d.DigitoConta)?.ToUpperInvariant();
		c.OP_BCO = Texto(d.Operacao);
		c.TP_CTA_COR = d.CdTipoConta;
		c.DE_TIT_COR = Texto(d.Titular);
		c.DE_PIX = Texto(d.ChavePix);
		c.NR_IDT = Texto(d.Identificador);
		c.NR_IDT_02 = Texto(d.IdentificadorCef);
		c.ID_CC_ATV = d.CdSituacao;
		c.ENV_OPT_CAX = RegrasContaBancaria.CodigoOptanteCef(d.EnviarOptanteCef);
	}

	/// <summary>
	/// Contas da pessoa (ou só uma, com sqConta). O filtro vem antes do Select.
	/// As descrições dos códigos fixos são preenchidas depois da consulta.
	/// </summary>
	private static async Task<List<ContaBancariaDto>> BuscarAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? sqConta,
		CancellationToken cancellationToken)
	{
		var contas = db.ContasBancarias
			.AsNoTracking()
			.Where(c => c.CD_EMP == emp && c.SQ_PES == sqPesCli);

		if (sqConta is decimal sequencia)
		{
			contas = contas.Where(c => c.SQ_CTA_COR == sequencia);
		}

		var linhas = await contas
			.OrderBy(c => c.SQ_CTA_COR)
			.Select(c => new
			{
				Conta = c,
				Banco = db.Bancos.Where(b => b.CD_EMP == c.CD_EMP && b.CD_BCO == c.CD_BCO).Select(b => b.NM_BCO).FirstOrDefault()
			})
			.ToListAsync(cancellationToken);

		return linhas.Select(l => ParaDto(l.Conta, l.Banco)).ToList();
	}

	private static ContaBancariaDto ParaDto(T000CCOR c, string? banco) => new()
	{
		SqConta = c.SQ_CTA_COR,
		CdBanco = c.CD_BCO,
		Banco = banco,
		DigitoBanco = c.DV_BCO,
		Agencia = c.CD_AG,
		DigitoAgencia = c.DV_AG,
		NumeroConta = c.NR_CTA_COR,
		DigitoConta = c.DV_CTA_COR,
		Operacao = c.OP_BCO,
		CdTipoConta = c.TP_CTA_COR,
		TipoConta = RegrasContaBancaria.DescricaoTipo(c.TP_CTA_COR),
		Titular = c.DE_TIT_COR,
		ChavePix = c.DE_PIX,
		Identificador = c.NR_IDT,
		IdentificadorCef = c.NR_IDT_02,
		CdSituacao = c.ID_CC_ATV,
		Situacao = RegrasContaBancaria.DescricaoSituacao(c.ID_CC_ATV),
		EnviarOptanteCef = RegrasContaBancaria.EnviarOptanteCef(c.ENV_OPT_CAX)
	};

	private static async Task<ContaBancariaDto?> ObterAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal sqConta,
		CancellationToken cancellationToken)
	{
		return (await BuscarAsync(db, emp, sqPesCli, sqConta, cancellationToken)).FirstOrDefault();
	}
}
