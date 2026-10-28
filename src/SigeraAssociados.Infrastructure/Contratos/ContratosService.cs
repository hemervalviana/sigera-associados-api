using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Contratos;
using SigeraAssociados.Infrastructure.Associados;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Contratos;

/// <summary>
/// Contratos (T009CTRT) com as regras da tela w_r_t009ctrt do legado.
/// SQ_CTR = MAX + 1 da filial (UPDLOCK, HOLDLOCK); a inclusão gera as
/// parcelas (T009PAGA) na mesma transação. Exclusão lógica.
/// </summary>
public sealed class ContratosService : IContratosService
{
	private const int TamanhoPaginaMaximo = 200;

	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public ContratosService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	// ===================== Pesquisa =====================

	private sealed class LinhaContrato
	{
		public decimal CdFil { get; init; }
		public decimal SqCtr { get; init; }
		public decimal SqPesCli { get; init; }
		public string? Nome { get; init; }
		public decimal? Cpf { get; init; }
		public decimal SqPesCorretor { get; init; }
		public string? Corretor { get; init; }
		public decimal? CdProduto { get; init; }
		public string? Produto { get; init; }
		public decimal? CdSituacao { get; init; }
		public string? Situacao { get; init; }
		public DateTime DataContrato { get; init; }
		public DateTime? DataPrimeiraParcela { get; init; }
		public decimal? ValorContrato { get; init; }
		public decimal? ValorMensalidade { get; init; }
		public decimal? QuantidadeParcelas { get; init; }
	}

	/// <inheritdoc />
	public async Task<PesquisaContratosResultado> PesquisarAsync(
		PesquisaContratosRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.Pagina < 1)
		{
			throw new DadosInvalidosException("A página deve ser maior ou igual a 1.");
		}

		if (request.TamanhoPagina < 1 || request.TamanhoPagina > TamanhoPaginaMaximo)
		{
			throw new DadosInvalidosException($"O tamanho da página deve estar entre 1 e {TamanhoPaginaMaximo}.");
		}

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);
		var vazio = new PesquisaContratosResultado { Pagina = request.Pagina, TamanhoPagina = request.TamanhoPagina };

		if (escopo.SemAcesso)
		{
			return vazio;
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			throw new DadosInvalidosException("Não foi possível identificar a empresa do sistema: a tabela T009EMPS deve ter exatamente uma empresa.");
		}

		var query = AcessoContrato.AplicarEscopo(AcessoContrato.Validos(db, emp).AsNoTracking(), escopo);

		if (request.SqPesCli is decimal cli) query = query.Where(c => c.SQ_PES_CLI == cli);
		if (request.CdFil is decimal fil) query = query.Where(c => c.CD_FIL == fil);
		if (request.CdSituacao is decimal sit) query = query.Where(c => c.CD_ST_CTR == sit);
		if (request.SqPesCorretor is decimal cro) query = query.Where(c => c.SQ_PES_CRO == cro);
		if (request.CdProduto is decimal prd) query = query.Where(c => c.CD_PRD == prd);

		if (!string.IsNullOrWhiteSpace(request.Busca))
		{
			var busca = request.Busca.Trim();
			var digitos = new string(busca.Where(char.IsAsciiDigit).ToArray());

			if (LerMascaraContrato(busca) is (decimal filialBusca, decimal numeroBusca))
			{
				query = query.Where(c => c.CD_FIL == filialBusca && c.SQ_CTR == numeroBusca);
			}
			else if (digitos.Length == 11 && busca.All(ch => char.IsAsciiDigit(ch) || ch is '.' or '-'))
			{
				var cpf = decimal.Parse(digitos);
				query = query.Where(c => db.PessoasFisicas.Any(f => f.CD_EMP == c.CD_EMP && f.SQ_PES_FIS == c.SQ_PES_CLI && f.NR_CPF == cpf));
			}
			else if (digitos.Length is > 0 and <= 8 && digitos.Length == busca.Length)
			{
				var numero = decimal.Parse(digitos);
				query = query.Where(c => c.SQ_CTR == numero);
			}
			else
			{
				query = query.Where(c => db.Pessoas.Any(p => p.CD_EMP == c.CD_EMP && p.SQ_PES == c.SQ_PES_CLI && p.NM_PES.StartsWith(busca)));
			}
		}

		var total = await query.CountAsync(cancellationToken);

		var linhas = await Projetar(db, query)
			.OrderByDescending(l => l.DataContrato)
			.ThenBy(l => l.Nome)
			.ThenByDescending(l => l.SqCtr)
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.ToListAsync(cancellationToken);

		return new PesquisaContratosResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = total,
			TotalPaginas = (int)Math.Ceiling(total / (double)request.TamanhoPagina),
			Contratos = linhas.Select(ParaResumo).ToList()
		};
	}

	/// <summary>
	/// Contrato na máscara do legado ##-###### (filial + número, ex.:
	/// 01-000123). Aceita menos dígitos em cada parte (1-123).
	/// </summary>
	internal static (decimal Filial, decimal Numero)? LerMascaraContrato(string texto)
	{
		var partes = texto.Trim().Split('-');

		return partes.Length == 2
			&& partes[0].Length is >= 1 and <= 2 && partes[0].All(char.IsAsciiDigit)
			&& partes[1].Length is >= 1 and <= 6 && partes[1].All(char.IsAsciiDigit)
				? (decimal.Parse(partes[0]), decimal.Parse(partes[1]))
				: null;
	}

	private static IQueryable<LinhaContrato> Projetar(SigeraDbContext db, IQueryable<T009CTRT> query) =>
		query.Select(c => new LinhaContrato
		{
			CdFil = c.CD_FIL,
			SqCtr = c.SQ_CTR,
			SqPesCli = c.SQ_PES_CLI,
			Nome = db.Pessoas.Where(p => p.CD_EMP == c.CD_EMP && p.SQ_PES == c.SQ_PES_CLI).Select(p => p.NM_PES).FirstOrDefault(),
			Cpf = db.PessoasFisicas.Where(f => f.CD_EMP == c.CD_EMP && f.SQ_PES_FIS == c.SQ_PES_CLI).Select(f => f.NR_CPF).FirstOrDefault(),
			SqPesCorretor = c.SQ_PES_CRO,
			Corretor = db.Pessoas.Where(p => p.CD_EMP == c.CD_EMP && p.SQ_PES == c.SQ_PES_CRO).Select(p => p.NM_PES).FirstOrDefault(),
			CdProduto = c.CD_PRD,
			Produto = db.Produtos.Where(p => p.CD_EMP == c.CD_EMP && p.CD_PRD == c.CD_PRD).Select(p => p.DE_PRD).FirstOrDefault(),
			CdSituacao = c.CD_ST_CTR,
			Situacao = db.SituacoesContrato.Where(s => s.CD_EMP == c.CD_EMP && s.CD_ST_CTR == c.CD_ST_CTR).Select(s => s.DE_ST_CTR).FirstOrDefault(),
			DataContrato = c.DT_CTR,
			DataPrimeiraParcela = c.DT_PRI_PCL,
			ValorContrato = c.VR_CTR,
			ValorMensalidade = c.VR_MNS,
			QuantidadeParcelas = c.QT_PCL
		});

	private static ContratoResumoDto ParaResumo(LinhaContrato l) => new()
	{
		CdFil = l.CdFil,
		SqCtr = l.SqCtr,
		SqPesCli = l.SqPesCli,
		NomeAssociado = l.Nome,
		CpfAssociado = DocumentoCpf.Formatar(l.Cpf),
		SqPesCorretor = l.SqPesCorretor,
		Corretor = l.Corretor,
		CdProduto = l.CdProduto,
		Produto = l.Produto,
		CdSituacao = l.CdSituacao,
		Situacao = l.Situacao,
		DataContrato = l.DataContrato,
		DataPrimeiraParcela = l.DataPrimeiraParcela,
		ValorContrato = l.ValorContrato,
		ValorMensalidade = l.ValorMensalidade,
		QuantidadeParcelas = l.QuantidadeParcelas
	};

	// ===================== Detalhe =====================

	/// <inheritdoc />
	public async Task<ContratoDto?> ObterAsync(decimal cdFil, decimal sqCtr, CancellationToken cancellationToken = default)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return null;
		}

		var contrato = await AcessoContrato.CarregarAsync(db, emp, cdFil, sqCtr, escopo, rastrear: false, cancellationToken);

		return contrato is null ? null : await ParaDtoAsync(db, contrato, cancellationToken);
	}

	// ===================== Inclusão =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ContratoDto>> IncluirAsync(
		ContratoDados dados,
		string login,
		CancellationToken cancellationToken = default)
	{
		if (RegrasContrato.Validar(dados, inclusao: true) is string erro)
		{
			return Invalido<ContratoDto>(erro);
		}

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);
		var cdFil = dados.CdFil!.Value;

		// Quem só tem a carteira de um corretor inclui contratos desse corretor.
		var avaliacao = EscopoEscrita.AvaliarInclusao(escopo, cdFil, dados.SqPesCorretor);

		if (!avaliacao.Permitido)
		{
			return avaliacao.FaltaEscolherCorretor
				? Invalido<ContratoDto>(avaliacao.Mensagem!)
				: Proibido<ContratoDto>((avaliacao.Mensagem ?? "Você não tem acesso para incluir contratos.").Replace("associados", "contratos"));
		}

		if (avaliacao.Corretor is not decimal corretor)
		{
			return Invalido<ContratoDto>("O corretor (sqPesCorretor) é obrigatório.");
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ContratoDto>();
		}

		var sqPesCli = dados.SqPesCli!.Value;

		if (!await AcessoAssociado.PermitidoAsync(db, emp, sqPesCli, escopo, cancellationToken))
		{
			return NaoEncontrado<ContratoDto>("Associado não encontrado.");
		}

		var quantidade = (int)dados.QuantidadeParcelas!.Value;
		var referencias = await ValidarReferenciasAsync(db, emp, cdFil, dados, corretor, corretorAtual: null, quantidade, cancellationToken);

		if (referencias.Erro is string erroReferencia)
		{
			return Invalido<ContratoDto>(erroReferencia);
		}

		var (indice, mensalidade) = RegrasContrato.IndiceEMensalidade(dados.ValorContrato!.Value, dados.Indice, dados.ValorMensalidade, referencias.IndiceTabela);

		if (mensalidade is not > 0)
		{
			return Invalido<ContratoDto>(
				"Não foi possível gerar as parcelas: informe a mensalidade (valorMensalidade), o índice, ou a carência com índice cadastrado para a quantidade de parcelas.");
		}

		if (ValidarCalculados(indice, mensalidade) is string erroCalculo)
		{
			return Invalido<ContratoDto>(erroCalculo);
		}

		var parcelas = RegrasContrato.GerarParcelas(dados.DataPrimeiraParcela!.Value, quantidade, mensalidade.Value, dados.ValorCoberturaPeculio);

		if (RegrasContrato.ValidarValorParcela(parcelas[0].Valor) is string erroParcela)
		{
			return Invalido<ContratoDto>(erroParcela);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqCtr = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_CTR), 0) + 1 AS [Value] FROM dbo.T009CTRT WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND CD_FIL = {cdFil}",
			cancellationToken);

		if (sqCtr > RegrasContrato.SequenciaMaxima)
		{
			return Invalido<ContratoDto>("A filial atingiu o limite de numeração de contratos.");
		}

		var agora = DateTime.Now;

		var contrato = new T009CTRT
		{
			CD_EMP = emp,
			CD_FIL = cdFil,
			SQ_CTR = sqCtr,
			SQ_PES_CLI = sqPesCli,
			CD_PRD = dados.CdProduto,
			DT_CTR = (dados.DataContrato ?? agora).Date,
			DT_CAD = agora,
			NM_USU = Truncar(login, 20),
			QT_PCL = quantidade,
			DT_ENC_CTR = RegrasContrato.DataEncerramento(null, dados.CdSituacao, null, agora)
		};

		Aplicar(dados, contrato, corretor, referencias.CdOrgao, indice, mensalidade);

		if (contrato.CD_PRD == RegrasContrato.ProdutoAuxilioFinanceiro)
		{
			contrato.DT_ULT_DCT = parcelas[^1].Vencimento;
		}

		db.Contratos.Add(contrato);

		foreach (var (numero, vencimento, valor) in parcelas)
		{
			db.Parcelas.Add(new T009PAGA
			{
				CD_EMP = emp,
				CD_FIL = cdFil,
				SQ_CTR = sqCtr,
				NR_PCL = numero,
				DT_VEN = vencimento,
				VR_PCL = valor,
				VR_SD_PCL = -valor,
				DT_GER_PCL = agora,
				NM_USU = Truncar(login, 50)
			});
		}

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ContratoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso($"Contrato incluído com {quantidade} parcela(s).", await ParaDtoAsync(db, contrato, cancellationToken));
	}

	// ===================== Alteração =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ContratoDto>> AlterarAsync(
		decimal cdFil,
		decimal sqCtr,
		ContratoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (RegrasContrato.Validar(dados, inclusao: false) is string erro)
		{
			return Invalido<ContratoDto>(erro);
		}

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ContratoDto>();
		}

		var contrato = await AcessoContrato.CarregarAsync(db, emp, cdFil, sqCtr, escopo, rastrear: true, cancellationToken);

		if (contrato is null)
		{
			return NaoEncontrado<ContratoDto>("Contrato não encontrado.");
		}

		if (Imutaveis(dados, contrato) is string erroImutavel)
		{
			return Invalido<ContratoDto>(erroImutavel);
		}

		if (RegrasContrato.PrimeiraParcela(dados.DataPrimeiraParcela, dados.DataContrato ?? contrato.DT_CTR) is string erroData)
		{
			return Invalido<ContratoDto>(erroData);
		}

		// Para quem só tem a carteira: o contrato continua de um corretor dos seus acessos.
		var avaliacao = EscopoEscrita.AvaliarInclusao(escopo, cdFil, dados.SqPesCorretor ?? contrato.SQ_PES_CRO);

		if (!avaliacao.Permitido)
		{
			return avaliacao.FaltaEscolherCorretor
				? Invalido<ContratoDto>(avaliacao.Mensagem!)
				: Proibido<ContratoDto>(avaliacao.Mensagem ?? "Você não tem acesso a este corretor.");
		}

		var corretor = avaliacao.Corretor ?? contrato.SQ_PES_CRO;
		var quantidade = (int)(contrato.QT_PCL ?? 0);

		var referencias = await ValidarReferenciasAsync(db, emp, cdFil, dados, corretor, contrato.SQ_PES_CRO, quantidade, cancellationToken);

		if (referencias.Erro is string erroReferencia)
		{
			return Invalido<ContratoDto>(erroReferencia);
		}

		var (indice, mensalidade) = RegrasContrato.IndiceEMensalidade(dados.ValorContrato!.Value, dados.Indice, dados.ValorMensalidade, referencias.IndiceTabela);

		if (ValidarCalculados(indice, mensalidade) is string erroCalculo)
		{
			return Invalido<ContratoDto>(erroCalculo);
		}

		contrato.DT_ENC_CTR = RegrasContrato.DataEncerramento(contrato.CD_ST_CTR, dados.CdSituacao, contrato.DT_ENC_CTR, DateTime.Now);

		if (dados.DataContrato is DateTime novaData)
		{
			contrato.DT_CTR = novaData.Date;
		}
		Aplicar(dados, contrato, corretor, referencias.CdOrgao, indice, mensalidade);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ContratoDto>(erroGravacao);
		}

		return Sucesso("Contrato alterado.", await ParaDtoAsync(db, contrato, cancellationToken));
	}

	/// <summary>Associado, produto e filial não mudam (o legado protege).</summary>
	private static string? Imutaveis(ContratoDados d, T009CTRT c)
	{
		if (d.SqPesCli != c.SQ_PES_CLI) return "O associado do contrato não pode ser alterado.";
		if (d.CdProduto != c.CD_PRD) return "O produto do contrato não pode ser alterado.";
		if (d.CdFil is decimal fil && fil != c.CD_FIL) return "A filial do contrato não pode ser alterada.";
		return null;
	}

	// ===================== Exclusão =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ContratoResumoDto>> ExcluirAsync(
		decimal cdFil,
		decimal sqCtr,
		string login,
		CancellationToken cancellationToken = default)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ContratoResumoDto>();
		}

		var contrato = await AcessoContrato.CarregarAsync(db, emp, cdFil, sqCtr, escopo, rastrear: true, cancellationToken);

		if (contrato is null)
		{
			return NaoEncontrado<ContratoResumoDto>("Contrato não encontrado.");
		}

		var resumo = ParaResumo(await Projetar(db, db.Contratos.AsNoTracking()
				.Where(c => c.CD_EMP == emp && c.CD_FIL == cdFil && c.SQ_CTR == sqCtr))
			.FirstAsync(cancellationToken));

		// Exclusão lógica: nada é apagado e a situação não muda.
		contrato.DT_EXC_API = DateTime.Now;
		contrato.NM_USU_EXC_API = Truncar(login, 256);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ContratoResumoDto>(erroGravacao)
			: Sucesso("Contrato excluído.", resumo);
	}

	// ===================== Tabelas de apoio =====================

	/// <inheritdoc />
	public Task<IReadOnlyList<ApoioContratoDto>> SituacoesAsync(CancellationToken cancellationToken = default) =>
		ListarApoioAsync((db, emp) => db.SituacoesContrato.AsNoTracking().Where(s => s.CD_EMP == emp).OrderBy(s => s.CD_ST_CTR)
			.Select(s => new ApoioContratoDto(s.CD_ST_CTR, s.DE_ST_CTR, null)), cancellationToken);

	/// <inheritdoc />
	public Task<IReadOnlyList<ApoioContratoDto>> CarenciasAsync(CancellationToken cancellationToken = default) =>
		ListarApoioAsync((db, emp) => db.Carencias.AsNoTracking().Where(s => s.CD_EMP == emp).OrderBy(s => s.CD_CAR)
			.Select(s => new ApoioContratoDto(s.CD_CAR, s.DE_CAR, null)), cancellationToken);

	/// <inheritdoc />
	public Task<IReadOnlyList<ApoioContratoDto>> OrigensRecursoAsync(CancellationToken cancellationToken = default) =>
		ListarApoioAsync((db, emp) => db.OrigensRecurso.AsNoTracking().Where(s => s.CD_EMP == emp).OrderBy(s => s.CD_ORI_REC)
			.Select(s => new ApoioContratoDto(s.CD_ORI_REC, s.DE_ORI_REC, s.TP_ORI_REC)), cancellationToken);

	/// <inheritdoc />
	public Task<IReadOnlyList<ApoioContratoDto>> ProdutoresAsync(decimal? sqPesCorretor, CancellationToken cancellationToken = default) =>
		ListarApoioAsync((db, emp) => db.Produtores.AsNoTracking()
			.Where(p => p.CD_EMP == emp && (sqPesCorretor == null || p.SQ_PES_CRO == sqPesCorretor))
			.OrderBy(p => p.NM_PRD)
			.Select(p => new ApoioContratoDto(p.SQ_PRD, p.NM_PRD, null)), cancellationToken);

	/// <inheritdoc />
	public async Task<IReadOnlyList<IndiceContratoDto>> IndicesAsync(
		decimal? cdCarencia,
		decimal? quantidadeParcelas,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return [];
		}

		return await db.Indices.AsNoTracking()
			.Where(i => i.CD_EMP == emp
				&& (cdCarencia == null || i.CD_CAR == cdCarencia)
				&& (quantidadeParcelas == null || i.QT_PCL == quantidadeParcelas))
			.OrderBy(i => i.CD_CAR).ThenBy(i => i.QT_PCL)
			.Select(i => new IndiceContratoDto(i.CD_CAR, i.QT_PCL, i.VR_IND))
			.ToListAsync(cancellationToken);
	}

	private async Task<IReadOnlyList<ApoioContratoDto>> ListarApoioAsync(
		Func<SigeraDbContext, decimal, IQueryable<ApoioContratoDto>> consulta,
		CancellationToken cancellationToken)
	{
		await using var db = CriarContexto();

		return await ObterEmpresaAsync(db, cancellationToken) is decimal emp
			? await consulta(db, emp).ToListAsync(cancellationToken)
			: [];
	}

	// ===================== Apoio =====================

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	private static string Truncar(string valor, int tamanho) =>
		valor.Length <= tamanho ? valor : valor[..tamanho];

	private static string? ValidarCalculados(decimal? indice, decimal? mensalidade) =>
		RegrasContrato.Monetario(mensalidade, 99_999.99m, "valorMensalidade (valor do contrato x índice)")
		?? (indice is > 9.9999m ? "O índice deve estar entre 0 e 9,9999." : null);

	private sealed record Referencias(string? Erro, decimal CdOrgao = 0, decimal? IndiceTabela = null);

	/// <summary>
	/// Confere cada código nas tabelas do sistema (as validações dos itemchanged).
	/// </summary>
	private static async Task<Referencias> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		decimal cdFil,
		ContratoDados d,
		decimal corretor,
		decimal? corretorAtual,
		int quantidadeParcelas,
		CancellationToken ct)
	{
		var cli = d.SqPesCli!.Value;

		if (!await db.Filiais.AnyAsync(f => f.CD_EMP == emp && f.CD_FIL == cdFil, ct))
			return new($"A filial {cdFil:0} não está cadastrada (T000PARA).");

		var produto = d.CdProduto!.Value;
		if (!await db.Produtos.AnyAsync(p => p.CD_EMP == emp && p.CD_PRD == produto, ct))
			return new($"O produto {produto:0} não está cadastrado (T009PRDT).");

		var convenio = d.CdConvenio!.Value;
		if (!await db.Convenios.AnyAsync(c => c.CD_EMP == emp && c.CD_CVN == convenio, ct))
			return new($"O convênio {convenio:0} não está cadastrado (T009CONV).");

		var forma = d.CdFormaPagamento!.Value;
		if (!await db.FormasPagamento.AnyAsync(f => f.CD_EMP == emp && f.TP_FMA_PGT == forma, ct))
			return new($"A forma de pagamento {forma:0} não está cadastrada (T009FRPG).");

		var situacao = d.CdSituacao!.Value;
		if (!await db.SituacoesContrato.AnyAsync(s => s.CD_EMP == emp && s.CD_ST_CTR == situacao, ct))
			return new($"A situação {situacao:0} não está cadastrada (T009SCTR).");

		var sqOrg = d.SqOrgao!.Value;
		var cdOrgao = await db.VinculosOrgao.AsNoTracking()
			.Where(v => v.CD_EMP == emp && v.SQ_PES == cli && v.SQ_ORG == sqOrg)
			.Select(v => (decimal?)v.CD_ORG)
			.FirstOrDefaultAsync(ct);
		if (cdOrgao is null)
			return new($"O vínculo de órgão {sqOrg:0} não é deste associado (T000RPOR).");

		var conta = d.SqContaAssociado!.Value;
		if (!await db.ContasBancarias.AnyAsync(c => c.CD_EMP == emp && c.SQ_PES == cli && c.SQ_CTA_COR == conta, ct))
			return new($"A conta bancária {conta:0} não é deste associado (T000CCOR).");

		if (d.CdMunicipio is decimal mun && !await db.Municipios.AnyAsync(m => m.CD_EMP == emp && m.CD_MUN == mun, ct))
			return new($"O município {mun:0} não está cadastrado (T000MUNI).");

		if (d.CdCarencia is decimal car && !await db.Carencias.AnyAsync(c => c.CD_EMP == emp && c.CD_CAR == car, ct))
			return new($"A carência {car:0} não está cadastrada (T009CARE).");

		if (d.SqConsignante is decimal ccg
			&& !await db.Associados.AnyAsync(a => a.CD_EMP == emp && a.SQ_PES_CLI == ccg && a.DT_EXC_API == null, ct))
			return new($"O consignante {ccg:0} não é um associado cadastrado.");

		// Corretor mantido na alteração vale mesmo que tenha sido excluído depois.
		if (corretor != corretorAtual
			&& !await db.Corretores.AnyAsync(c => c.CD_EMP == emp && c.SQ_PES_CRO == corretor && c.DT_EXC_API == null, ct))
			return new($"O corretor {corretor:0} não está cadastrado.");

		if (d.SqContaCorretor is decimal contaCro
			&& !await db.ContasBancarias.AnyAsync(c => c.CD_EMP == emp && c.SQ_PES == corretor && c.SQ_CTA_COR == contaCro, ct))
			return new($"A conta bancária {contaCro:0} não é do corretor {corretor:0} (T000CCOR).");

		var categoria = d.CdCategoria!.Value;
		if (!await db.Categorias.AnyAsync(c => c.CD_EMP == emp && c.CD_CAT == categoria, ct))
			return new($"A categoria {categoria:0} não está cadastrada (T009CAT).");

		var temDeposito = d.CdProduto != RegrasContrato.ProdutoPlanoAssociativo;

		if (temDeposito && d.CdOrigemRecurso is decimal ori && !await db.OrigensRecurso.AnyAsync(o => o.CD_EMP == emp && o.CD_ORI_REC == ori, ct))
			return new($"A origem do recurso {ori:0} não está cadastrada (T009OREC).");

		if (d.SqProdutor is decimal prd
			&& !await db.Produtores.AnyAsync(p => p.CD_EMP == emp && p.SQ_PRD == prd && p.SQ_PES_CRO == corretor, ct))
			return new($"O produtor {prd:0} não está cadastrado para o corretor {corretor:0} (T009PROD).");

		if (temDeposito && d.CdBanco is decimal bco && !await db.Bancos.AnyAsync(b => b.CD_EMP == emp && b.CD_BCO == bco, ct))
			return new($"O banco {bco:0} não está cadastrado (T000BANC).");

		// wf_buscar_indice: só quando nem índice nem mensalidade vieram.
		decimal? indiceTabela = null;

		if (d.Indice is null && d.ValorMensalidade is null && d.CdCarencia is decimal carencia && quantidadeParcelas > 0)
		{
			indiceTabela = await db.Indices.AsNoTracking()
				.Where(i => i.CD_EMP == emp && i.CD_CAR == carencia && i.QT_PCL == quantidadeParcelas)
				.Select(i => (decimal?)i.VR_IND)
				.FirstOrDefaultAsync(ct);

			if (indiceTabela is null)
				return new($"Não há índice cadastrado (T009INDI) para a carência {carencia:0} com {quantidadeParcelas} parcela(s): informe o índice ou a mensalidade.");
		}

		return new(null, cdOrgao.Value, indiceTabela);
	}

	/// <summary>Grava os campos editáveis (os mesmos na inclusão e na alteração).</summary>
	private static void Aplicar(ContratoDados d, T009CTRT c, decimal corretor, decimal cdOrgao, decimal? indice, decimal? mensalidade)
	{
		c.CD_CVN = d.CdConvenio;
		c.TP_FMA_PGT = d.CdFormaPagamento!.Value;
		c.CD_ST_CTR = d.CdSituacao;
		c.SQ_ORG = d.SqOrgao;
		c.CD_ORG = cdOrgao;
		c.SQ_CTA_COR = d.SqContaAssociado;
		c.CD_MUN = d.CdMunicipio;
		c.DT_PRI_PCL = d.DataPrimeiraParcela!.Value.Date;
		c.VR_CTR = d.ValorContrato;
		c.CD_CAR = d.CdCarencia;
		c.VR_IND = indice;
		c.VR_MNS = mensalidade;
		c.VR_CBT_PEC = d.ValorCoberturaPeculio;
		c.SQ_PES_CCG = d.SqConsignante;
		c.NR_PRP = d.NumeroProposta;
		c.VR_PGT_FORA = d.ValorPagoFora;
		c.DT_QIT_CTRT = d.DataQuitacao?.Date;
		c.VR_QIT_CTR = d.ValorQuitacao;
		c.DT_PRV_QIT = d.DataPrevisaoQuitacao?.Date;
		c.DT_FIN_CTR = d.DataFinalizacao?.Date;
		c.DT_CAN_CTRT = d.DataCancelamento?.Date;
		c.VR_CMP_DIV = d.ValorCompraDivida;
		c.VR_CMP_DIV_TP = RegrasContrato.TipoCompraDividaGravado(d.ValorCompraDivida, d.TipoCompraDivida);
		c.CD_TP_CTRT = d.CdTipoContrato;
		c.VR_TAR_BCO = d.ValorTarifaBancaria;
		c.VR_DSP_ADM = d.ValorDespesaAdministrativa;
		c.VR_INA = d.ValorIna;
		c.OB_CTR = Texto(d.Observacao);
		c.OB_SNT = Texto(d.ObservacaoSnt);
		c.SQ_PES_CRO = corretor;
		c.SQ_CTA_COR_CRRT = d.SqContaCorretor;
		c.CD_CAT = d.CdCategoria;
		c.SQ_PRD = d.SqProdutor;
		c.CD_ENT_FISI = d.CdEntFisi;

		// Comissão (VR_CMS, DT_PGT_CMS): só leitura nesta rota; nada é gravado.

		// Depósito ao associado: o PLANO ASSOCIATIVO não tem; nele os campos
		// enviados são ignorados (inclusão: nulos; alteração: mantém o gravado).
		if (c.CD_PRD != RegrasContrato.ProdutoPlanoAssociativo)
		{
			c.CD_ORI_REC = d.CdOrigemRecurso;
			c.DT_PGT_AUX = d.DataPagamentoAuxilio?.Date;
			c.VR_DEP = d.ValorDeposito;
			c.CD_BCO = d.CdBanco;
		}
	}

	private static async Task<ContratoDto> ParaDtoAsync(SigeraDbContext db, T009CTRT c, CancellationToken ct)
	{
		var emp = c.CD_EMP;

		async Task<string?> NomeAsync(decimal? sq) => sq is decimal v
			? await db.Pessoas.AsNoTracking().Where(p => p.CD_EMP == emp && p.SQ_PES == v).Select(p => p.NM_PES).FirstOrDefaultAsync(ct)
			: null;

		var parcelas = await ParcelasContratoService.CarregarAsync(db, c, ct);

		return new ContratoDto
		{
			CdFil = c.CD_FIL,
			SqCtr = c.SQ_CTR,
			SqPesCli = c.SQ_PES_CLI,
			NomeAssociado = await NomeAsync(c.SQ_PES_CLI),
			CpfAssociado = DocumentoCpf.Formatar(await db.PessoasFisicas.AsNoTracking()
				.Where(f => f.CD_EMP == emp && f.SQ_PES_FIS == c.SQ_PES_CLI).Select(f => f.NR_CPF).FirstOrDefaultAsync(ct)),
			CdProduto = c.CD_PRD,
			Produto = await db.Produtos.AsNoTracking().Where(p => p.CD_EMP == emp && p.CD_PRD == c.CD_PRD).Select(p => p.DE_PRD).FirstOrDefaultAsync(ct),
			CdConvenio = c.CD_CVN,
			Convenio = await db.Convenios.AsNoTracking().Where(v => v.CD_EMP == emp && v.CD_CVN == c.CD_CVN).Select(v => v.NM_CVN).FirstOrDefaultAsync(ct),
			CdFormaPagamento = c.TP_FMA_PGT,
			FormaPagamento = await db.FormasPagamento.AsNoTracking().Where(f => f.CD_EMP == emp && f.TP_FMA_PGT == c.TP_FMA_PGT).Select(f => f.DE_FMA_PGT).FirstOrDefaultAsync(ct),
			CdSituacao = c.CD_ST_CTR,
			Situacao = await db.SituacoesContrato.AsNoTracking().Where(s => s.CD_EMP == emp && s.CD_ST_CTR == c.CD_ST_CTR).Select(s => s.DE_ST_CTR).FirstOrDefaultAsync(ct),
			SqOrgao = c.SQ_ORG,
			CdOrgao = c.CD_ORG,
			Orgao = await db.Orgaos.AsNoTracking().Where(o => o.CD_EMP == emp && o.CD_ORG == c.CD_ORG).Select(o => o.NM_ORG).FirstOrDefaultAsync(ct),
			SqContaAssociado = c.SQ_CTA_COR,
			CdMunicipio = c.CD_MUN,
			Municipio = await db.Municipios.AsNoTracking().Where(m => m.CD_EMP == emp && m.CD_MUN == c.CD_MUN).Select(m => m.DE_MUN).FirstOrDefaultAsync(ct),
			DataContrato = c.DT_CTR,
			DataPrimeiraParcela = c.DT_PRI_PCL,
			ValorContrato = c.VR_CTR,
			QuantidadeParcelas = c.QT_PCL,
			CdCarencia = c.CD_CAR,
			Carencia = await db.Carencias.AsNoTracking().Where(x => x.CD_EMP == emp && x.CD_CAR == c.CD_CAR).Select(x => x.DE_CAR).FirstOrDefaultAsync(ct),
			Indice = c.VR_IND,
			ValorMensalidade = c.VR_MNS,
			ValorCoberturaPeculio = c.VR_CBT_PEC,
			SqConsignante = c.SQ_PES_CCG,
			Consignante = await NomeAsync(c.SQ_PES_CCG),
			NumeroProposta = c.NR_PRP,
			ValorPagoFora = c.VR_PGT_FORA,
			DataQuitacao = c.DT_QIT_CTRT,
			ValorQuitacao = c.VR_QIT_CTR,
			DataPrevisaoQuitacao = c.DT_PRV_QIT,
			DataFinalizacao = c.DT_FIN_CTR,
			DataCancelamento = c.DT_CAN_CTRT,
			DataEncerramento = c.DT_ENC_CTR,
			DataUltimoDesconto = c.DT_ULT_DCT,
			ValorCompraDivida = c.VR_CMP_DIV,
			TipoCompraDivida = c.VR_CMP_DIV_TP,
			DescricaoTipoCompraDivida = RegrasContrato.DescricaoTipoCompraDivida(c.VR_CMP_DIV_TP),
			CdTipoContrato = c.CD_TP_CTRT,
			DescricaoTipoContrato = RegrasContrato.DescricaoTipoContrato(c.CD_TP_CTRT),
			ValorTarifaBancaria = c.VR_TAR_BCO,
			ValorDespesaAdministrativa = c.VR_DSP_ADM,
			ValorIna = c.VR_INA,
			Observacao = c.OB_CTR,
			ObservacaoSnt = c.OB_SNT,
			SqPesCorretor = c.SQ_PES_CRO,
			Corretor = await NomeAsync(c.SQ_PES_CRO),
			SqContaCorretor = c.SQ_CTA_COR_CRRT,
			CdCategoria = c.CD_CAT,
			Categoria = await db.Categorias.AsNoTracking().Where(x => x.CD_EMP == emp && x.CD_CAT == c.CD_CAT).Select(x => x.NM_CAT).FirstOrDefaultAsync(ct),
			CdOrigemRecurso = c.CD_ORI_REC,
			OrigemRecurso = await db.OrigensRecurso.AsNoTracking().Where(o => o.CD_EMP == emp && o.CD_ORI_REC == c.CD_ORI_REC).Select(o => o.DE_ORI_REC).FirstOrDefaultAsync(ct),
			SqProdutor = c.SQ_PRD,
			Produtor = await db.Produtores.AsNoTracking().Where(p => p.CD_EMP == emp && p.SQ_PRD == c.SQ_PRD).Select(p => p.NM_PRD).FirstOrDefaultAsync(ct),
			ValorComissao = c.VR_CMS,
			DataPagamentoComissao = c.DT_PGT_CMS,
			DataPagamentoAuxilio = c.DT_PGT_AUX,
			ValorDeposito = c.VR_DEP,
			CdBanco = c.CD_BCO,
			Banco = await db.Bancos.AsNoTracking().Where(b => b.CD_EMP == emp && b.CD_BCO == c.CD_BCO).Select(b => b.NM_BCO).FirstOrDefaultAsync(ct),
			CdEntFisi = c.CD_ENT_FISI,
			DescricaoEntregaFisica = RegrasContrato.DescricaoEntregaFisica(c.CD_ENT_FISI),
			DataCadastro = c.DT_CAD,
			UsuarioCadastro = c.NM_USU,
			Parcelas = parcelas
		};
	}
}
