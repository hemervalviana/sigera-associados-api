using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Corretores;
using SigeraAssociados.Application.Interfaces.Corretores;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Corretores;

/// <summary>
/// Agentes (T009AGENT) e gerentes de vendas (T009GVED) no sistema da
/// requisição. Código = MAX + 1 com UPDLOCK, HOLDLOCK quando omitido.
/// </summary>
public sealed class TabelasApoioCorretorService : ITabelasApoioCorretorService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;

	public TabelasApoioCorretorService(SigeraDbContextFactory factory, SigeraSystemContext sistema)
	{
		_factory = factory;
		_sistema = sistema;
	}

	// ===================== Agentes (T009AGENT) =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<AgenteDados>> ListarAgentesAsync(string? busca, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var query = db.Agentes.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(a => a.SQ_AGEN == codigo || (a.DE_AGEN != null && a.DE_AGEN.StartsWith(texto)))
				: query.Where(a => a.DE_AGEN != null && a.DE_AGEN.StartsWith(texto));
		}

		return (await query.OrderBy(a => a.DE_AGEN).ThenBy(a => a.SQ_AGEN).Take(500).ToListAsync(cancellationToken))
			.Select(ParaDados)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<AgenteDados>> IncluirAgenteAsync(AgenteDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioCorretor.Agente(dados, inclusao: true) is string erro)
		{
			return Invalido<AgenteDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<AgenteDados>();
		}

		if (await BancoInexistenteAsync(db, emp, dados.CdBanco, cancellationToken) is string erroBanco)
		{
			return Invalido<AgenteDados>(erroBanco);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.SqAgente ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_AGEN), 0) + 1 AS [Value] FROM dbo.T009AGENT WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoApoioCorretor.CodigoMaximo)
		{
			return Invalido<AgenteDados>("Não há código de agente livre.");
		}

		if (await db.Agentes.AnyAsync(a => a.CD_EMP == emp && a.SQ_AGEN == codigo, cancellationToken))
		{
			return Conflito<AgenteDados>($"O agente {codigo:0} já existe.");
		}

		var agente = new T009AGENT { CD_EMP = emp, SQ_AGEN = codigo };
		Aplicar(dados, agente);
		db.Agentes.Add(agente);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<AgenteDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Agente cadastrado.", ParaDados(agente));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<AgenteDados>> AlterarAgenteAsync(decimal sqAgente, AgenteDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioCorretor.Agente(dados, inclusao: false) is string erro)
		{
			return Invalido<AgenteDados>(erro);
		}

		await using var db = CriarContexto();

		var agente = await db.Agentes.FirstOrDefaultAsync(a => a.SQ_AGEN == sqAgente, cancellationToken);

		if (agente is null)
		{
			return NaoEncontrado<AgenteDados>("Agente não encontrado.");
		}

		if (await BancoInexistenteAsync(db, agente.CD_EMP, dados.CdBanco, cancellationToken) is string erroBanco)
		{
			return Invalido<AgenteDados>(erroBanco);
		}

		Aplicar(dados, agente);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<AgenteDados>(erroGravacao)
			: Sucesso("Agente alterado.", ParaDados(agente));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<AgenteDados>> ExcluirAgenteAsync(decimal sqAgente, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var agente = await db.Agentes.FirstOrDefaultAsync(a => a.SQ_AGEN == sqAgente, cancellationToken);

		if (agente is null)
		{
			return NaoEncontrado<AgenteDados>("Agente não encontrado.");
		}

		// Inclui corretores excluídos pela API: a linha continua em T009CRRT (FK).
		if (await db.Corretores.AnyAsync(c => c.CD_EMP == agente.CD_EMP && c.SQ_AGEN == sqAgente, cancellationToken))
		{
			return Conflito<AgenteDados>($"O agente {sqAgente:0} não pode ser excluído: há corretores com ele.");
		}

		var dto = ParaDados(agente);
		db.Agentes.Remove(agente);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<AgenteDados>(erroGravacao)
			: Sucesso("Agente excluído.", dto);
	}

	private static void Aplicar(AgenteDados d, T009AGENT a)
	{
		a.DE_AGEN = d.Descricao!.Trim();
		a.CD_BCO = d.CdBanco;
		a.DV_BCO = Texto(d.DigitoBanco)?.ToUpperInvariant();
		a.CD_AG = d.Agencia;
		a.DV_AG = Texto(d.DigitoAgencia)?.ToUpperInvariant();
		a.NR_CTA_COR = d.Conta;
		a.DV_CTA_COR = Texto(d.DigitoConta)?.ToUpperInvariant();
		a.OP_BCO = d.Operacao;
	}

	private static AgenteDados ParaDados(T009AGENT a) => new()
	{
		SqAgente = a.SQ_AGEN,
		Descricao = a.DE_AGEN,
		CdBanco = a.CD_BCO,
		DigitoBanco = a.DV_BCO,
		Agencia = a.CD_AG,
		DigitoAgencia = a.DV_AG,
		Conta = a.NR_CTA_COR,
		DigitoConta = a.DV_CTA_COR,
		Operacao = a.OP_BCO
	};

	private static async Task<string?> BancoInexistenteAsync(SigeraDbContext db, decimal emp, decimal? cdBanco, CancellationToken cancellationToken)
	{
		if (cdBanco is not decimal banco)
		{
			return null;
		}

		return await db.Bancos.AnyAsync(b => b.CD_EMP == emp && b.CD_BCO == banco, cancellationToken)
			? null
			: $"O banco {banco:0} não está cadastrado (T000BANC).";
	}

	// ===================== Gerentes de vendas (T009GVED) =====================

	/// <inheritdoc />
	public async Task<IReadOnlyList<GerenteVendasDados>> ListarGerentesVendasAsync(string? busca, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var query = db.GerentesVendas.AsNoTracking();

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(g => g.SQ_GVED == codigo || (g.DE_GVED != null && g.DE_GVED.StartsWith(texto)))
				: query.Where(g => g.DE_GVED != null && g.DE_GVED.StartsWith(texto));
		}

		return (await query.OrderBy(g => g.DE_GVED).ThenBy(g => g.SQ_GVED).Take(500).ToListAsync(cancellationToken))
			.Select(ParaDados)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<GerenteVendasDados>> IncluirGerenteVendasAsync(GerenteVendasDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioCorretor.GerenteVendas(dados, inclusao: true) is string erro)
		{
			return Invalido<GerenteVendasDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<GerenteVendasDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.SqGerenteVendas ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_GVED), 0) + 1 AS [Value] FROM dbo.T009GVED WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoApoioCorretor.CodigoMaximo)
		{
			return Invalido<GerenteVendasDados>("Não há código de gerente de vendas livre.");
		}

		if (await db.GerentesVendas.AnyAsync(g => g.CD_EMP == emp && g.SQ_GVED == codigo, cancellationToken))
		{
			return Conflito<GerenteVendasDados>($"O gerente de vendas {codigo:0} já existe.");
		}

		var gerente = new T009GVED { CD_EMP = emp, SQ_GVED = codigo };
		Aplicar(dados, gerente);
		db.GerentesVendas.Add(gerente);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<GerenteVendasDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Gerente de vendas cadastrado.", ParaDados(gerente));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<GerenteVendasDados>> AlterarGerenteVendasAsync(decimal sqGerenteVendas, GerenteVendasDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioCorretor.GerenteVendas(dados, inclusao: false) is string erro)
		{
			return Invalido<GerenteVendasDados>(erro);
		}

		await using var db = CriarContexto();

		var gerente = await db.GerentesVendas.FirstOrDefaultAsync(g => g.SQ_GVED == sqGerenteVendas, cancellationToken);

		if (gerente is null)
		{
			return NaoEncontrado<GerenteVendasDados>("Gerente de vendas não encontrado.");
		}

		Aplicar(dados, gerente);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<GerenteVendasDados>(erroGravacao)
			: Sucesso("Gerente de vendas alterado.", ParaDados(gerente));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<GerenteVendasDados>> ExcluirGerenteVendasAsync(decimal sqGerenteVendas, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var gerente = await db.GerentesVendas.FirstOrDefaultAsync(g => g.SQ_GVED == sqGerenteVendas, cancellationToken);

		if (gerente is null)
		{
			return NaoEncontrado<GerenteVendasDados>("Gerente de vendas não encontrado.");
		}

		// Sem FK no banco: a API confere antes de excluir.
		if (await db.Corretores.AnyAsync(c => c.CD_EMP == gerente.CD_EMP && c.SQ_GVED == sqGerenteVendas, cancellationToken))
		{
			return Conflito<GerenteVendasDados>($"O gerente de vendas {sqGerenteVendas:0} não pode ser excluído: há corretores com ele.");
		}

		var dto = ParaDados(gerente);
		db.GerentesVendas.Remove(gerente);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<GerenteVendasDados>(erroGravacao)
			: Sucesso("Gerente de vendas excluído.", dto);
	}

	private static void Aplicar(GerenteVendasDados d, T009GVED g)
	{
		g.DE_GVED = d.Descricao!.Trim();
		g.VR_PC_CMS = d.PercentualComissao;
	}

	private static GerenteVendasDados ParaDados(T009GVED g) => new()
	{
		SqGerenteVendas = g.SQ_GVED,
		Descricao = g.DE_GVED,
		PercentualComissao = g.VR_PC_CMS
	};

	// ----- Apoio -----

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}
}
