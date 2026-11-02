using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Contratos;
using SigeraAssociados.Application.Interfaces.Contratos;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Contratos;

/// <summary>
/// Gravação das tabelas de apoio do contrato. Código omitido = MAX + 1
/// (UPDLOCK, HOLDLOCK). Exclusão física, recusada quando há contrato (ou
/// índice, no caso da carência) usando o registro, inclusive contrato
/// excluído pela API, que continua no banco.
/// </summary>
public sealed class TabelasApoioContratoService : ITabelasApoioContratoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;

	public TabelasApoioContratoService(SigeraDbContextFactory factory, SigeraSystemContext sistema)
	{
		_factory = factory;
		_sistema = sistema;
	}

	// ===================== Situações (T009SCTR) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<SituacaoContratoApoioDados>> IncluirSituacaoAsync(SituacaoContratoApoioDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Situacao(dados, inclusao: true) is string erro)
		{
			return Invalido<SituacaoContratoApoioDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<SituacaoContratoApoioDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdSituacao ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_ST_CTR), 0) + 1 AS [Value] FROM dbo.T009SCTR WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoApoioContrato.SituacaoMaxima)
		{
			return Invalido<SituacaoContratoApoioDados>("Não há código de situação livre (máximo 99).");
		}

		if (await db.SituacoesContrato.AnyAsync(x => x.CD_EMP == emp && x.CD_ST_CTR == codigo, cancellationToken))
		{
			return Conflito<SituacaoContratoApoioDados>($"A situação {codigo:0} já existe.");
		}

		var situacao = new T009SCTR { CD_EMP = emp, CD_ST_CTR = codigo, DE_ST_CTR = ValidacaoApoioContrato.Texto(dados.Descricao!) };
		db.SituacoesContrato.Add(situacao);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<SituacaoContratoApoioDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);
		return Sucesso("Situação cadastrada.", ParaDados(situacao));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<SituacaoContratoApoioDados>> AlterarSituacaoAsync(decimal cdSituacao, SituacaoContratoApoioDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Situacao(dados, inclusao: false) is string erro)
		{
			return Invalido<SituacaoContratoApoioDados>(erro);
		}

		await using var db = CriarContexto();

		var situacao = await BuscarAsync(db, d => d.SituacoesContrato, x => x.CD_ST_CTR == cdSituacao, cancellationToken);

		if (situacao is null)
		{
			return NaoEncontrado<SituacaoContratoApoioDados>("Situação não encontrada.");
		}

		// Só a descrição muda; o código (inclusive dos fixos) fica.
		situacao.DE_ST_CTR = ValidacaoApoioContrato.Texto(dados.Descricao!);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<SituacaoContratoApoioDados>(erroGravacao)
			: Sucesso("Situação alterada.", ParaDados(situacao));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<SituacaoContratoApoioDados>> ExcluirSituacaoAsync(decimal cdSituacao, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.SituacoesFixas.Contains(cdSituacao))
		{
			return Conflito<SituacaoContratoApoioDados>(
				$"A situação {cdSituacao:0} é usada pela API (aprovado, renovação, encerrado, quitado, cancelado, finalizado ou quitado por dentro) e não pode ser excluída; só a descrição pode ser alterada.");
		}

		await using var db = CriarContexto();

		var situacao = await BuscarAsync(db, d => d.SituacoesContrato, x => x.CD_ST_CTR == cdSituacao, cancellationToken);

		if (situacao is null)
		{
			return NaoEncontrado<SituacaoContratoApoioDados>("Situação não encontrada.");
		}

		if (await db.Contratos.AnyAsync(c => c.CD_EMP == situacao.CD_EMP && c.CD_ST_CTR == cdSituacao, cancellationToken))
		{
			return Conflito<SituacaoContratoApoioDados>($"A situação {cdSituacao:0} não pode ser excluída: há contratos com ela.");
		}

		var dto = ParaDados(situacao);
		db.SituacoesContrato.Remove(situacao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<SituacaoContratoApoioDados>(erroGravacao)
			: Sucesso("Situação excluída.", dto);
	}

	private static SituacaoContratoApoioDados ParaDados(T009SCTR x) => new() { CdSituacao = x.CD_ST_CTR, Descricao = x.DE_ST_CTR };

	// ===================== Carências (T009CARE) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CarenciaDados>> IncluirCarenciaAsync(CarenciaDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Carencia(dados, inclusao: true) is string erro)
		{
			return Invalido<CarenciaDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<CarenciaDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdCarencia ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_CAR), 0) + 1 AS [Value] FROM dbo.T009CARE WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoApoioContrato.CarenciaMaxima)
		{
			return Invalido<CarenciaDados>("Não há código de carência livre (máximo 9).");
		}

		if (await db.Carencias.AnyAsync(c => c.CD_EMP == emp && c.CD_CAR == codigo, cancellationToken))
		{
			return Conflito<CarenciaDados>($"A carência {codigo:0} já existe.");
		}

		var carencia = new T009CARE { CD_EMP = emp, CD_CAR = codigo, DE_CAR = ValidacaoApoioContrato.Texto(dados.Descricao!) };
		db.Carencias.Add(carencia);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<CarenciaDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);
		return Sucesso("Carência cadastrada.", ParaDados(carencia));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CarenciaDados>> AlterarCarenciaAsync(decimal cdCarencia, CarenciaDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Carencia(dados, inclusao: false) is string erro)
		{
			return Invalido<CarenciaDados>(erro);
		}

		await using var db = CriarContexto();

		var carencia = await BuscarAsync(db, d => d.Carencias, c => c.CD_CAR == cdCarencia, cancellationToken);

		if (carencia is null)
		{
			return NaoEncontrado<CarenciaDados>("Carência não encontrada.");
		}

		carencia.DE_CAR = ValidacaoApoioContrato.Texto(dados.Descricao!);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<CarenciaDados>(erroGravacao)
			: Sucesso("Carência alterada.", ParaDados(carencia));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CarenciaDados>> ExcluirCarenciaAsync(decimal cdCarencia, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var carencia = await BuscarAsync(db, d => d.Carencias, c => c.CD_CAR == cdCarencia, cancellationToken);

		if (carencia is null)
		{
			return NaoEncontrado<CarenciaDados>("Carência não encontrada.");
		}

		if (await db.Contratos.AnyAsync(c => c.CD_EMP == carencia.CD_EMP && c.CD_CAR == cdCarencia, cancellationToken))
		{
			return Conflito<CarenciaDados>($"A carência {cdCarencia:0} não pode ser excluída: há contratos com ela.");
		}

		if (await db.Indices.AnyAsync(i => i.CD_EMP == carencia.CD_EMP && i.CD_CAR == cdCarencia, cancellationToken))
		{
			return Conflito<CarenciaDados>($"A carência {cdCarencia:0} não pode ser excluída: há índices cadastrados para ela (exclua os índices antes).");
		}

		var dto = ParaDados(carencia);
		db.Carencias.Remove(carencia);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<CarenciaDados>(erroGravacao)
			: Sucesso("Carência excluída.", dto);
	}

	private static CarenciaDados ParaDados(T009CARE c) => new() { CdCarencia = c.CD_CAR, Descricao = c.DE_CAR };

	// ===================== Origens do recurso (T009OREC) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<OrigemRecursoDados>> IncluirOrigemRecursoAsync(OrigemRecursoDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.OrigemRecurso(dados, inclusao: true) is string erro)
		{
			return Invalido<OrigemRecursoDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<OrigemRecursoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdOrigemRecurso ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_ORI_REC), 0) + 1 AS [Value] FROM dbo.T009OREC WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoApoioContrato.OrigemMaxima)
		{
			return Invalido<OrigemRecursoDados>("Não há código de origem do recurso livre.");
		}

		if (await db.OrigensRecurso.AnyAsync(o => o.CD_EMP == emp && o.CD_ORI_REC == codigo, cancellationToken))
		{
			return Conflito<OrigemRecursoDados>($"A origem do recurso {codigo:0} já existe.");
		}

		var origem = new T009OREC { CD_EMP = emp, CD_ORI_REC = codigo };
		Aplicar(dados, origem);
		db.OrigensRecurso.Add(origem);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<OrigemRecursoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);
		return Sucesso("Origem do recurso cadastrada.", ParaDados(origem));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<OrigemRecursoDados>> AlterarOrigemRecursoAsync(decimal cdOrigemRecurso, OrigemRecursoDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.OrigemRecurso(dados, inclusao: false) is string erro)
		{
			return Invalido<OrigemRecursoDados>(erro);
		}

		await using var db = CriarContexto();

		var origem = await BuscarAsync(db, d => d.OrigensRecurso, o => o.CD_ORI_REC == cdOrigemRecurso, cancellationToken);

		if (origem is null)
		{
			return NaoEncontrado<OrigemRecursoDados>("Origem do recurso não encontrada.");
		}

		Aplicar(dados, origem);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<OrigemRecursoDados>(erroGravacao)
			: Sucesso("Origem do recurso alterada.", ParaDados(origem));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<OrigemRecursoDados>> ExcluirOrigemRecursoAsync(decimal cdOrigemRecurso, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var origem = await BuscarAsync(db, d => d.OrigensRecurso, o => o.CD_ORI_REC == cdOrigemRecurso, cancellationToken);

		if (origem is null)
		{
			return NaoEncontrado<OrigemRecursoDados>("Origem do recurso não encontrada.");
		}

		if (await db.Contratos.AnyAsync(c => c.CD_EMP == origem.CD_EMP && c.CD_ORI_REC == cdOrigemRecurso, cancellationToken))
		{
			return Conflito<OrigemRecursoDados>($"A origem do recurso {cdOrigemRecurso:0} não pode ser excluída: há contratos com ela.");
		}

		var dto = ParaDados(origem);
		db.OrigensRecurso.Remove(origem);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<OrigemRecursoDados>(erroGravacao)
			: Sucesso("Origem do recurso excluída.", dto);
	}

	private static void Aplicar(OrigemRecursoDados d, T009OREC o)
	{
		o.DE_ORI_REC = ValidacaoApoioContrato.Texto(d.Descricao!);
		o.TP_ORI_REC = ValidacaoApoioContrato.Codigo(d.Tipo!);
	}

	private static OrigemRecursoDados ParaDados(T009OREC o) => new()
	{
		CdOrigemRecurso = o.CD_ORI_REC,
		Descricao = o.DE_ORI_REC,
		Tipo = o.TP_ORI_REC
	};

	// ===================== Produtores (T009PROD) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ProdutorDados>> IncluirProdutorAsync(ProdutorDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Produtor(dados, inclusao: true) is string erro)
		{
			return Invalido<ProdutorDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ProdutorDados>();
		}

		if (await CorretorAsync(db, emp, dados.SqPesCorretor!.Value, cancellationToken) is string erroCorretor)
		{
			return Invalido<ProdutorDados>(erroCorretor);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.SqProdutor ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_PRD), 0) + 1 AS [Value] FROM dbo.T009PROD WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoApoioContrato.ProdutorMaximo)
		{
			return Invalido<ProdutorDados>("Não há código de produtor livre.");
		}

		if (await db.Produtores.AnyAsync(p => p.CD_EMP == emp && p.SQ_PRD == codigo, cancellationToken))
		{
			return Conflito<ProdutorDados>($"O produtor {codigo:0} já existe.");
		}

		var produtor = new T009PROD
		{
			CD_EMP = emp,
			SQ_PRD = codigo,
			SQ_PES_CRO = dados.SqPesCorretor!.Value,
			NM_PRD = ValidacaoApoioContrato.Texto(dados.Descricao!)
		};
		db.Produtores.Add(produtor);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ProdutorDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);
		return Sucesso("Produtor cadastrado.", await ParaDadosAsync(db, produtor, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ProdutorDados>> AlterarProdutorAsync(decimal sqProdutor, ProdutorDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Produtor(dados, inclusao: false) is string erro)
		{
			return Invalido<ProdutorDados>(erro);
		}

		await using var db = CriarContexto();

		var produtor = await BuscarAsync(db, d => d.Produtores, p => p.SQ_PRD == sqProdutor, cancellationToken);

		if (produtor is null)
		{
			return NaoEncontrado<ProdutorDados>("Produtor não encontrado.");
		}

		var corretor = dados.SqPesCorretor!.Value;

		if (corretor != produtor.SQ_PES_CRO)
		{
			// O contrato exige que o produtor seja do corretor do contrato:
			// trocar o corretor de um produtor em uso deixaria contratos incoerentes.
			if (await db.Contratos.AnyAsync(c => c.CD_EMP == produtor.CD_EMP && c.SQ_PRD == sqProdutor, cancellationToken))
			{
				return Conflito<ProdutorDados>($"O corretor do produtor {sqProdutor:0} não pode mudar: há contratos com ele.");
			}

			if (await CorretorAsync(db, produtor.CD_EMP, corretor, cancellationToken) is string erroCorretor)
			{
				return Invalido<ProdutorDados>(erroCorretor);
			}

			produtor.SQ_PES_CRO = corretor;
		}

		produtor.NM_PRD = ValidacaoApoioContrato.Texto(dados.Descricao!);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<ProdutorDados>(erroGravacao)
			: Sucesso("Produtor alterado.", await ParaDadosAsync(db, produtor, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ProdutorDados>> ExcluirProdutorAsync(decimal sqProdutor, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var produtor = await BuscarAsync(db, d => d.Produtores, p => p.SQ_PRD == sqProdutor, cancellationToken);

		if (produtor is null)
		{
			return NaoEncontrado<ProdutorDados>("Produtor não encontrado.");
		}

		if (await db.Contratos.AnyAsync(c => c.CD_EMP == produtor.CD_EMP && c.SQ_PRD == sqProdutor, cancellationToken))
		{
			return Conflito<ProdutorDados>($"O produtor {sqProdutor:0} não pode ser excluído: há contratos com ele.");
		}

		var dto = await ParaDadosAsync(db, produtor, cancellationToken);
		db.Produtores.Remove(produtor);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ProdutorDados>(erroGravacao)
			: Sucesso("Produtor excluído.", dto);
	}

	private static async Task<string?> CorretorAsync(SigeraDbContext db, decimal emp, decimal corretor, CancellationToken cancellationToken) =>
		await db.Corretores.AnyAsync(c => c.CD_EMP == emp && c.SQ_PES_CRO == corretor && c.DT_EXC_API == null, cancellationToken)
			? null
			: $"O corretor {corretor:0} não está cadastrado.";

	private static async Task<ProdutorDados> ParaDadosAsync(SigeraDbContext db, T009PROD p, CancellationToken cancellationToken) => new()
	{
		SqProdutor = p.SQ_PRD,
		Descricao = p.NM_PRD,
		SqPesCorretor = p.SQ_PES_CRO,
		Corretor = await db.Pessoas.AsNoTracking()
			.Where(x => x.CD_EMP == p.CD_EMP && x.SQ_PES == p.SQ_PES_CRO)
			.Select(x => x.NM_PES)
			.FirstOrDefaultAsync(cancellationToken)
	};

	// ===================== Índices (T009INDI) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<IndiceContratoDto>> IncluirIndiceAsync(IndiceDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Indice(dados, inclusao: true) is string erro)
		{
			return Invalido<IndiceContratoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<IndiceContratoDto>();
		}

		var carencia = dados.CdCarencia!.Value;
		var parcelas = dados.QuantidadeParcelas!.Value;

		if (!await db.Carencias.AnyAsync(c => c.CD_EMP == emp && c.CD_CAR == carencia, cancellationToken))
		{
			return Invalido<IndiceContratoDto>($"A carência {carencia:0} não está cadastrada.");
		}

		if (await db.Indices.AnyAsync(i => i.CD_EMP == emp && i.CD_CAR == carencia && i.QT_PCL == parcelas, cancellationToken))
		{
			return Conflito<IndiceContratoDto>($"Já existe índice para a carência {carencia:0} com {parcelas:0} parcela(s): altere o existente.");
		}

		var indice = new T009INDI { CD_EMP = emp, CD_CAR = carencia, QT_PCL = parcelas, VR_IND = dados.Indice!.Value };
		db.Indices.Add(indice);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<IndiceContratoDto>(erroGravacao)
			: Sucesso("Índice cadastrado.", ParaDto(indice));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<IndiceContratoDto>> AlterarIndiceAsync(decimal cdCarencia, decimal quantidadeParcelas, IndiceDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoApoioContrato.Indice(dados, inclusao: false) is string erro)
		{
			return Invalido<IndiceContratoDto>(erro);
		}

		await using var db = CriarContexto();

		var indice = await BuscarAsync(db, d => d.Indices, i => i.CD_CAR == cdCarencia && i.QT_PCL == quantidadeParcelas, cancellationToken);

		if (indice is null)
		{
			return NaoEncontrado<IndiceContratoDto>("Índice não encontrado.");
		}

		// Só o índice muda; os contratos já gravados mantêm a mensalidade deles.
		indice.VR_IND = dados.Indice!.Value;

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<IndiceContratoDto>(erroGravacao)
			: Sucesso("Índice alterado.", ParaDto(indice));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<IndiceContratoDto>> ExcluirIndiceAsync(decimal cdCarencia, decimal quantidadeParcelas, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var indice = await BuscarAsync(db, d => d.Indices, i => i.CD_CAR == cdCarencia && i.QT_PCL == quantidadeParcelas, cancellationToken);

		if (indice is null)
		{
			return NaoEncontrado<IndiceContratoDto>("Índice não encontrado.");
		}

		// O índice só é lido na inclusão do contrato (o contrato guarda o valor):
		// excluir não afeta contratos já gravados.
		var dto = ParaDto(indice);
		db.Indices.Remove(indice);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<IndiceContratoDto>(erroGravacao)
			: Sucesso("Índice excluído.", dto);
	}

	private static IndiceContratoDto ParaDto(T009INDI i) => new(i.CD_CAR, i.QT_PCL, i.VR_IND);

	// ===================== Apoio =====================

	/// <summary>
	/// Registro rastreado, ou null. A base tem uma empresa só e o código é
	/// único dentro dela; a chave completa inclui CD_EMP, mas o filtro pelo
	/// código basta.
	/// </summary>
	private static Task<T?> BuscarAsync<T>(
		SigeraDbContext db,
		Func<SigeraDbContext, DbSet<T>> tabela,
		System.Linq.Expressions.Expression<Func<T, bool>> filtro,
		CancellationToken cancellationToken)
		where T : class =>
		tabela(db).FirstOrDefaultAsync(filtro, cancellationToken);

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}
}
