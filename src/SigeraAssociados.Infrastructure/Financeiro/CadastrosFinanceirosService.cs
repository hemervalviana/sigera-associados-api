using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Financeiro;
using SigeraAssociados.Application.Interfaces.Financeiro;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Financeiro;

/// <summary>
/// Formas de pagamento (T009FRPG), produtos (T009PRDT) e convênios (T009CONV)
/// no banco do sistema informado.
/// </summary>
public sealed class CadastrosFinanceirosService : ICadastrosFinanceirosService
{
	private readonly SigeraDbContextFactory _factory;

	public CadastrosFinanceirosService(SigeraDbContextFactory factory)
	{
		_factory = factory;
	}

	// ===================== Formas de pagamento (T009FRPG) =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<FormaPagamentoDados>> ListarFormasPagamentoAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		return (await db.FormasPagamento.AsNoTracking().OrderBy(f => f.TP_FMA_PGT).ToListAsync(cancellationToken))
			.Select(ParaDados)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<FormaPagamentoDados>> IncluirFormaPagamentoAsync(
		int sistema,
		FormaPagamentoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoFormaPagamento.Validar(dados, inclusao: true) is string erro)
		{
			return Invalido<FormaPagamentoDados>(erro);
		}

		await using var db = Abrir(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<FormaPagamentoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(TP_FMA_PGT), 0) + 1 AS [Value] FROM dbo.T009FRPG WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoFormaPagamento.CodigoMaximo)
		{
			return Invalido<FormaPagamentoDados>("Não há código de forma de pagamento livre (o máximo é 99).");
		}

		if (await db.FormasPagamento.AnyAsync(f => f.CD_EMP == emp && f.TP_FMA_PGT == codigo, cancellationToken))
		{
			return Conflito<FormaPagamentoDados>($"A forma de pagamento {codigo:0} já existe.");
		}

		var forma = new T009FRPG { CD_EMP = emp, TP_FMA_PGT = codigo };
		Aplicar(dados, forma);
		db.FormasPagamento.Add(forma);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<FormaPagamentoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Forma de pagamento cadastrada.", ParaDados(forma));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<FormaPagamentoDados>> AlterarFormaPagamentoAsync(
		int sistema,
		decimal codigo,
		FormaPagamentoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoFormaPagamento.Validar(dados, inclusao: false) is string erro)
		{
			return Invalido<FormaPagamentoDados>(erro);
		}

		await using var db = Abrir(sistema);

		var forma = await db.FormasPagamento.FirstOrDefaultAsync(f => f.TP_FMA_PGT == codigo, cancellationToken);

		if (forma is null)
		{
			return NaoEncontrado<FormaPagamentoDados>("Forma de pagamento não encontrada.");
		}

		Aplicar(dados, forma);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<FormaPagamentoDados>(erroGravacao)
			: Sucesso("Forma de pagamento alterada.", ParaDados(forma));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<FormaPagamentoDados>> ExcluirFormaPagamentoAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		var forma = await db.FormasPagamento.FirstOrDefaultAsync(f => f.TP_FMA_PGT == codigo, cancellationToken);

		if (forma is null)
		{
			return NaoEncontrado<FormaPagamentoDados>("Forma de pagamento não encontrada.");
		}

		if (await db.Creditos.AnyAsync(c => c.CD_EMP == forma.CD_EMP && c.TP_PGT == codigo, cancellationToken))
		{
			return Conflito<FormaPagamentoDados>($"A forma de pagamento {codigo:0} não pode ser excluída: há créditos de associados com ela.");
		}

		var dto = ParaDados(forma);
		db.FormasPagamento.Remove(forma);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<FormaPagamentoDados>(erroGravacao)
			: Sucesso("Forma de pagamento excluída.", dto);
	}

	private static void Aplicar(FormaPagamentoDados d, T009FRPG f)
	{
		f.DE_FMA_PGT = d.Descricao!.Trim();
		f.ID_GER_SLD = d.GeraSaldo == true ? ValidacaoFormaPagamento.GeraSaldo : ValidacaoFormaPagamento.NaoGeraSaldo;
	}

	private static FormaPagamentoDados ParaDados(T009FRPG f) => new()
	{
		Codigo = f.TP_FMA_PGT,
		Descricao = f.DE_FMA_PGT,
		GeraSaldo = f.ID_GER_SLD == ValidacaoFormaPagamento.GeraSaldo
	};

	// ===================== Produtos (T009PRDT) =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<ProdutoDados>> ListarProdutosAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		return await db.Produtos
			.AsNoTracking()
			.OrderBy(p => p.CD_PRD)
			.Select(p => new ProdutoDados { Codigo = p.CD_PRD, Descricao = p.DE_PRD })
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ProdutoDados>> IncluirProdutoAsync(
		int sistema,
		ProdutoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoProduto.Validar(dados, inclusao: true) is string erro)
		{
			return Invalido<ProdutoDados>(erro);
		}

		await using var db = Abrir(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ProdutoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_PRD), 0) + 1 AS [Value] FROM dbo.T009PRDT WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoProduto.CodigoMaximo)
		{
			return Invalido<ProdutoDados>("Não há código de produto livre (o máximo é 99).");
		}

		if (await db.Produtos.AnyAsync(p => p.CD_EMP == emp && p.CD_PRD == codigo, cancellationToken))
		{
			return Conflito<ProdutoDados>($"O produto {codigo:0} já existe.");
		}

		var produto = new T009PRDT { CD_EMP = emp, CD_PRD = codigo, DE_PRD = dados.Descricao!.Trim() };
		db.Produtos.Add(produto);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ProdutoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Produto cadastrado.", new ProdutoDados { Codigo = produto.CD_PRD, Descricao = produto.DE_PRD });
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ProdutoDados>> AlterarProdutoAsync(
		int sistema,
		decimal codigo,
		ProdutoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoProduto.Validar(dados, inclusao: false) is string erro)
		{
			return Invalido<ProdutoDados>(erro);
		}

		await using var db = Abrir(sistema);

		var produto = await db.Produtos.FirstOrDefaultAsync(p => p.CD_PRD == codigo, cancellationToken);

		if (produto is null)
		{
			return NaoEncontrado<ProdutoDados>("Produto não encontrado.");
		}

		produto.DE_PRD = dados.Descricao!.Trim();

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<ProdutoDados>(erroGravacao)
			: Sucesso("Produto alterado.", new ProdutoDados { Codigo = produto.CD_PRD, Descricao = produto.DE_PRD });
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ProdutoDados>> ExcluirProdutoAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		var produto = await db.Produtos.FirstOrDefaultAsync(p => p.CD_PRD == codigo, cancellationToken);

		if (produto is null)
		{
			return NaoEncontrado<ProdutoDados>("Produto não encontrado.");
		}

		if (await db.Creditos.AnyAsync(c => c.CD_EMP == produto.CD_EMP && c.CD_PRD == codigo, cancellationToken))
		{
			return Conflito<ProdutoDados>($"O produto {codigo:0} não pode ser excluído: há créditos de associados com ele.");
		}

		var dto = new ProdutoDados { Codigo = produto.CD_PRD, Descricao = produto.DE_PRD };
		db.Produtos.Remove(produto);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ProdutoDados>(erroGravacao)
			: Sucesso("Produto excluído.", dto);
	}

	// ===================== Convênios (T009CONV) =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<ConvenioResumoDto>> ListarConveniosAsync(
		int sistema,
		string? busca,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		var query = db.Convenios.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(c => c.CD_CVN == codigo || c.NM_CVN.StartsWith(texto))
				: query.Where(c => c.NM_CVN.StartsWith(texto));
		}

		return await query
			.OrderBy(c => c.NM_CVN)
			.ThenBy(c => c.CD_CVN)
			.Select(c => new ConvenioResumoDto
			{
				CdCvn = c.CD_CVN,
				Nome = c.NM_CVN,
				Numero = c.NR_CVN,
				CdFil = c.CD_FIL,
				CdBanco = c.CD_BCO,
				CdSituacao = c.CD_SIT_CON
			})
			.ToListAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ConvenioDados?> ObterConvenioAsync(
		int sistema,
		decimal cdCvn,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		var convenio = await db.Convenios.AsNoTracking().FirstOrDefaultAsync(c => c.CD_CVN == cdCvn, cancellationToken);

		return convenio is null ? null : ParaDados(convenio);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ConvenioDados>> IncluirConvenioAsync(
		int sistema,
		ConvenioDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoConvenio.Validar(dados, inclusao: true) is string erro)
		{
			return Invalido<ConvenioDados>(erro);
		}

		await using var db = Abrir(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ConvenioDados>();
		}

		if (await ValidarReferenciasAsync(db, emp, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<ConvenioDados>(erroReferencia);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdCvn ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_CVN), 0) + 1 AS [Value] FROM dbo.T009CONV WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoConvenio.CodigoMaximo)
		{
			return Invalido<ConvenioDados>("Não há código de convênio livre (o máximo é 999).");
		}

		if (await db.Convenios.AnyAsync(c => c.CD_EMP == emp && c.CD_CVN == codigo, cancellationToken))
		{
			return Conflito<ConvenioDados>($"O convênio {codigo:0} já existe.");
		}

		var convenio = new T009CONV { CD_EMP = emp, CD_CVN = codigo };
		Aplicar(dados, convenio);
		db.Convenios.Add(convenio);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ConvenioDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Convênio cadastrado.", ParaDados(convenio));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ConvenioDados>> AlterarConvenioAsync(
		int sistema,
		decimal cdCvn,
		ConvenioDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoConvenio.Validar(dados, inclusao: false) is string erro)
		{
			return Invalido<ConvenioDados>(erro);
		}

		await using var db = Abrir(sistema);

		var convenio = await db.Convenios.FirstOrDefaultAsync(c => c.CD_CVN == cdCvn, cancellationToken);

		if (convenio is null)
		{
			return NaoEncontrado<ConvenioDados>("Convênio não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, convenio.CD_EMP, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<ConvenioDados>(erroReferencia);
		}

		Aplicar(dados, convenio);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<ConvenioDados>(erroGravacao)
			: Sucesso("Convênio alterado.", ParaDados(convenio));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ConvenioDados>> ExcluirConvenioAsync(
		int sistema,
		decimal cdCvn,
		CancellationToken cancellationToken = default)
	{
		await using var db = Abrir(sistema);

		var convenio = await db.Convenios.FirstOrDefaultAsync(c => c.CD_CVN == cdCvn, cancellationToken);

		if (convenio is null)
		{
			return NaoEncontrado<ConvenioDados>("Convênio não encontrado.");
		}

		var emp = convenio.CD_EMP;

		if (await db.Creditos.AnyAsync(c => c.CD_EMP == emp && c.CD_CVN == cdCvn, cancellationToken))
		{
			return Conflito<ConvenioDados>($"O convênio {cdCvn:0} não pode ser excluído: há créditos de associados com ele.");
		}

		var averbacoes = await NumeroAsync(db,
			$"SELECT CAST(COUNT(*) AS decimal(10, 0)) AS [Value] FROM dbo.T009RBSR WHERE CD_EMP = {emp} AND CD_CVN = {cdCvn}",
			cancellationToken);

		if (averbacoes > 0)
		{
			return Conflito<ConvenioDados>($"O convênio {cdCvn:0} não pode ser excluído: há averbações (T009RBSR) nele.");
		}

		var dto = ParaDados(convenio);
		db.Convenios.Remove(convenio);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ConvenioDados>(erroGravacao)
			: Sucesso("Convênio excluído.", dto);
	}

	/// <summary>
	/// Confere filial (T000PARA), banco (T000BANC) e conta corrente (T009CONT).
	/// </summary>
	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		ConvenioDados dados,
		CancellationToken cancellationToken)
	{
		if (dados.CdFil is decimal filial
			&& !await db.Filiais.AnyAsync(f => f.CD_EMP == emp && f.CD_FIL == filial, cancellationToken))
		{
			return $"A filial {filial:0} não está cadastrada (T000PARA).";
		}

		if (dados.CdBanco is decimal banco
			&& !await db.Bancos.AnyAsync(b => b.CD_EMP == emp && b.CD_BCO == banco, cancellationToken))
		{
			return $"O banco {banco:0} não está cadastrado (T000BANC).";
		}

		if (dados.SqContaCorrente is decimal conta)
		{
			var contas = await NumeroAsync(db,
				$"SELECT CAST(COUNT(*) AS decimal(10, 0)) AS [Value] FROM dbo.T009CONT WHERE CD_EMP = {emp} AND SQ_CTA_COR = {conta}",
				cancellationToken);

			if (contas == 0)
			{
				return $"A conta corrente {conta:0} não está cadastrada (T009CONT).";
			}
		}

		return null;
	}

	private static void Aplicar(ConvenioDados d, T009CONV c)
	{
		c.NM_CVN = d.Nome!.Trim();
		c.NR_CVN = Texto(d.Numero);
		c.NR_CV2 = Texto(d.Numero2);
		c.NR_CV3 = Texto(d.Numero3);
		c.CD_FIL = d.CdFil;
		c.CD_BCO = d.CdBanco;
		c.SQ_CTA_COR = d.SqContaCorrente;
		c.CD_EMPR = d.CodigoEmpresa;
		c.CD_CART = d.Carteira;
		c.CD_COMP = d.CodigoCompensacao;
		c.CD_CNAB = d.CodigoCnab;
		c.PAR_TRSM = d.ParametroTransmissao;
		c.CT_TP_GOV = d.TipoGoverno;
		// Caminhos, extensão e ODBC são gravados como digitados (podem diferenciar maiúsculas).
		c.NM_DRT_REM = Texto(d.DiretorioRemessa);
		c.NM_DRT_ROR = Texto(d.DiretorioRetorno);
		c.DE_EXT_ARQ = Texto(d.ExtensaoArquivo);
		c.DE_ODBC = Texto(d.Odbc);
		c.CD_SIT_CON = d.CdSituacao;
	}

	private static ConvenioDados ParaDados(T009CONV c) => new()
	{
		CdCvn = c.CD_CVN,
		Nome = c.NM_CVN,
		Numero = c.NR_CVN,
		Numero2 = c.NR_CV2,
		Numero3 = c.NR_CV3,
		CdFil = c.CD_FIL,
		CdBanco = c.CD_BCO,
		SqContaCorrente = c.SQ_CTA_COR,
		CodigoEmpresa = c.CD_EMPR,
		Carteira = c.CD_CART,
		CodigoCompensacao = c.CD_COMP,
		CodigoCnab = c.CD_CNAB,
		ParametroTransmissao = c.PAR_TRSM,
		TipoGoverno = c.CT_TP_GOV,
		DiretorioRemessa = c.NM_DRT_REM,
		DiretorioRetorno = c.NM_DRT_ROR,
		ExtensaoArquivo = c.DE_EXT_ARQ,
		Odbc = c.DE_ODBC,
		CdSituacao = c.CD_SIT_CON
	};

	// ----- Apoio -----

	private SigeraDbContext Abrir(int sistema)
	{
		if (!SigeraSystemCatalog.TryGetDatabase(sistema, out _))
		{
			throw new DadosInvalidosException("Sistema inválido. Os valores permitidos são 1, 2, 3 e 4.");
		}

		return _factory.Create(sistema);
	}
}
