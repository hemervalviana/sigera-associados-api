using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Associados;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Associados;

/// <summary>
/// Grava associados pessoa física nas tabelas do legado:
/// T000PESS (pessoa), T000PFIS (pessoa física) e T000CLIE (associado).
///
/// - Inclusão numa transação, com SQ_PES = MAX + 1 lido com
///   UPDLOCK, HOLDLOCK (um segundo cadastro espera o primeiro terminar).
/// - Alteração grava só as colunas que mudaram; ao passar a situação
///   para 10 (falecido), a trigger TR_CLIE_UPD encerra os contratos ativos.
/// - Exclusão lógica em T000CLIE (DT_EXC_API, NM_USU_EXC_API).
/// - Tudo limitado pelo escopo de acesso do usuário (EscopoEscrita).
/// </summary>
public sealed class CadastroAssociadosService : ICadastroAssociadosService
{
	private const string TipoPessoaFisica = "F";
	private const int TentativasNumeracao = 3;

	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public CadastroAssociadosService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<AssociadoCadastroDto?> ObterAsync(
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso)
		{
			return null;
		}

		await using var db = CriarContexto();

		var empresa = await ObterEmpresaAsync(db, cancellationToken);

		if (empresa is null)
		{
			return null;
		}

		var cadastro = await CarregarAsync(db, empresa.Value, sqPesCli, rastrear: false, cancellationToken);

		if (cadastro is null
			|| !EscopoEscrita.PermiteAssociado(escopo, cadastro.Associado.CD_FIL, cadastro.Corretores))
		{
			return null;
		}

		return ParaDto(cadastro.Pessoa, cadastro.PessoaFisica, cadastro.Associado,
			await DescricaoSituacaoAsync(db, empresa.Value, cadastro.Associado.CD_SIT_CLI, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoCadastroAssociado> IncluirAsync(
		CadastroAssociadoDados dados,
		ExecutorCadastro executor,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(executor);

		if (ValidacaoCadastroAssociado.Validar(dados) is string erro)
		{
			return Invalido(erro);
		}

		var pf = dados.PessoaFisica!;
		var associado = dados.Associado!;

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		var avaliacao = EscopoEscrita.AvaliarInclusao(escopo, associado.CdFil, dados.SqPesCorretor);

		if (!avaliacao.Permitido)
		{
			return new ResultadoCadastroAssociado(
				avaliacao.FaltaEscolherCorretor
					? StatusCadastroAssociado.Invalido
					: StatusCadastroAssociado.Proibido,
				avaliacao.Mensagem);
		}

		if (associado.CdSitCli == ValidacaoCadastroAssociado.SituacaoFalecido
			&& !executor.PodeMarcarFalecido)
		{
			return ProibidoFalecido();
		}

		var cpf = NumeroCpf(pf.Cpf);

		for (var tentativa = 1; ; tentativa++)
		{
			await using var db = CriarContexto();

			var empresa = await ObterEmpresaAsync(db, cancellationToken);

			if (empresa is not decimal emp)
			{
				return EmpresaIndefinida();
			}

			if (await ValidarReferenciasAsync(db, emp, dados, avaliacao.Corretor, cancellationToken)
				is string erroReferencia)
			{
				return Invalido(erroReferencia);
			}

			var dono = await db.PessoasFisicas
				.AsNoTracking()
				.Where(f => f.CD_EMP == emp && f.NR_CPF == cpf)
				.Select(f => (decimal?)f.SQ_PES_FIS)
				.FirstOrDefaultAsync(cancellationToken);

			if (dono is not null)
			{
				return CpfRepetido(dono.Value);
			}

			await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

			try
			{
				var sqPes = await ProximoSqPesAsync(db, emp, cancellationToken);
				var agora = DateTime.Now;

				var pessoa = new T000PESS
				{
					CD_EMP = emp,
					SQ_PES = sqPes,
					TP_PES = TipoPessoaFisica,
					DT_CAD = agora,
					NM_USU = Truncar(executor.Login, 20)
				};

				var pessoaFisica = new T000PFIS { CD_EMP = emp, SQ_PES_FIS = sqPes };

				var cliente = new T000CLIE
				{
					CD_EMP = emp,
					SQ_PES_CLI = sqPes,
					SQ_PES_CRO_CAD = avaliacao.Corretor
				};

				Aplicar(dados, pessoa, pessoaFisica, cliente);

				// T000PESS primeiro: T000PFIS e T000CLIE têm FK para ela.
				db.Pessoas.Add(pessoa);
				await db.SaveChangesAsync(cancellationToken);

				db.PessoasFisicas.Add(pessoaFisica);
				db.Associados.Add(cliente);
				await db.SaveChangesAsync(cancellationToken);

				await transacao.CommitAsync(cancellationToken);

				return new ResultadoCadastroAssociado(
					StatusCadastroAssociado.Sucesso,
					"Associado cadastrado.",
					ParaDto(pessoa, pessoaFisica, cliente,
						await DescricaoSituacaoAsync(db, emp, cliente.CD_SIT_CLI, cancellationToken)));
			}
			catch (DbUpdateException ex) when (ChaveDuplicada(ex) && tentativa < TentativasNumeracao)
			{
				// Outro cadastro pegou o mesmo SQ_PES: desfaz e tenta com o próximo.
				await transacao.RollbackAsync(cancellationToken);
			}
		}
	}

	/// <inheritdoc />
	public async Task<ResultadoCadastroAssociado> AlterarAsync(
		decimal sqPesCli,
		CadastroAssociadoDados dados,
		ExecutorCadastro executor,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(executor);

		if (ValidacaoCadastroAssociado.Validar(dados) is string erro)
		{
			return Invalido(erro);
		}

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso)
		{
			return NaoEncontrado();
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida();
		}

		var cadastro = await CarregarAsync(db, emp, sqPesCli, rastrear: true, cancellationToken);

		if (cadastro is null
			|| !EscopoEscrita.PermiteAssociado(escopo, cadastro.Associado.CD_FIL, cadastro.Corretores))
		{
			return NaoEncontrado();
		}

		var novo = dados.Associado!;

		if (novo.CdFil != cadastro.Associado.CD_FIL
			&& !EscopoEscrita.PermiteAssociado(escopo, novo.CdFil, cadastro.Corretores))
		{
			return new ResultadoCadastroAssociado(
				StatusCadastroAssociado.Proibido,
				$"Você não tem acesso para mover o associado para a filial {novo.CdFil:0}.");
		}

		var marcandoFalecido =
			novo.CdSitCli == ValidacaoCadastroAssociado.SituacaoFalecido
			&& cadastro.Associado.CD_SIT_CLI != ValidacaoCadastroAssociado.SituacaoFalecido;

		if (marcandoFalecido && !executor.PodeMarcarFalecido)
		{
			return ProibidoFalecido();
		}

		// O vínculo de corretor do cadastro não muda na alteração.
		if (await ValidarReferenciasAsync(db, emp, dados, null, cancellationToken)
			is string erroReferencia)
		{
			return Invalido(erroReferencia);
		}

		var cpf = NumeroCpf(dados.PessoaFisica!.Cpf);

		if (cpf != cadastro.PessoaFisica.NR_CPF)
		{
			var dono = await db.PessoasFisicas
				.AsNoTracking()
				.Where(f => f.CD_EMP == emp && f.NR_CPF == cpf && f.SQ_PES_FIS != sqPesCli)
				.Select(f => (decimal?)f.SQ_PES_FIS)
				.FirstOrDefaultAsync(cancellationToken);

			if (dono is not null)
			{
				return CpfRepetido(dono.Value);
			}
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		int? contratosEncerrados = null;

		if (marcandoFalecido)
		{
			// Mesma condição da trigger TR_CLIE_UPD.
			contratosEncerrados = (await db.Database
					.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.T009CTRT WHERE SQ_PES_CLI = {sqPesCli} AND CD_ST_CTR IN (2, 5)")
					.ToListAsync(cancellationToken))
				.Single();
		}

		Aplicar(dados, cadastro.Pessoa, cadastro.PessoaFisica, cadastro.Associado);

		// O Entity Framework só inclui no UPDATE as colunas alteradas:
		// reenviar a mesma situação não dispara a regra da trigger.
		await db.SaveChangesAsync(cancellationToken);
		await transacao.CommitAsync(cancellationToken);

		var mensagem = marcandoFalecido
			? $"Associado alterado e marcado como falecido; {contratosEncerrados} contrato(s) ativo(s) encerrado(s)."
			: "Associado alterado.";

		return new ResultadoCadastroAssociado(
			StatusCadastroAssociado.Sucesso,
			mensagem,
			ParaDto(cadastro.Pessoa, cadastro.PessoaFisica, cadastro.Associado,
				await DescricaoSituacaoAsync(db, emp, cadastro.Associado.CD_SIT_CLI, cancellationToken)),
			contratosEncerrados);
	}

	/// <inheritdoc />
	public async Task<ResultadoCadastroAssociado> ExcluirAsync(
		decimal sqPesCli,
		ExecutorCadastro executor,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(executor);

		var escopo = await _escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso)
		{
			return NaoEncontrado();
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida();
		}

		var cadastro = await CarregarAsync(db, emp, sqPesCli, rastrear: true, cancellationToken);

		if (cadastro is null
			|| !EscopoEscrita.PermiteAssociado(escopo, cadastro.Associado.CD_FIL, cadastro.Corretores))
		{
			return NaoEncontrado();
		}

		// Exclusão lógica: CD_SIT_CLI não é tocado, então a trigger não age.
		cadastro.Associado.DT_EXC_API = DateTime.Now;
		cadastro.Associado.NM_USU_EXC_API = Truncar(executor.Login, 256);

		await db.SaveChangesAsync(cancellationToken);

		return new ResultadoCadastroAssociado(StatusCadastroAssociado.Sucesso, "Associado excluído.");
	}

	// ----- Apoio -----

	private sealed record Cadastro(
		T000PESS Pessoa,
		T000PFIS PessoaFisica,
		T000CLIE Associado,
		IReadOnlyCollection<decimal> Corretores);

	/// <summary>
	/// Carrega as três tabelas de um associado pessoa física não excluído,
	/// com os corretores ligados a ele (contratos e cadastro).
	/// </summary>
	private static async Task<Cadastro?> CarregarAsync(
		SigeraDbContext db,
		decimal empresa,
		decimal sqPesCli,
		bool rastrear,
		CancellationToken cancellationToken)
	{
		var associados = rastrear ? db.Associados : db.Associados.AsNoTracking();
		var pessoas = rastrear ? db.Pessoas : db.Pessoas.AsNoTracking();
		var pessoasFisicas = rastrear ? db.PessoasFisicas : db.PessoasFisicas.AsNoTracking();

		var associado = await associados.SingleOrDefaultAsync(
			a => a.CD_EMP == empresa && a.SQ_PES_CLI == sqPesCli && a.DT_EXC_API == null,
			cancellationToken);

		if (associado is null)
		{
			return null;
		}

		var pessoa = await pessoas.SingleOrDefaultAsync(
			p => p.CD_EMP == empresa && p.SQ_PES == sqPesCli && p.TP_PES == TipoPessoaFisica,
			cancellationToken);

		var pessoaFisica = await pessoasFisicas.SingleOrDefaultAsync(
			f => f.CD_EMP == empresa && f.SQ_PES_FIS == sqPesCli,
			cancellationToken);

		// Pessoa jurídica ou cadastro incompleto: fora do CRUD da API.
		if (pessoa is null || pessoaFisica is null)
		{
			return null;
		}

		var corretores = await db.Contratos
			.AsNoTracking()
			.Where(c => c.CD_EMP == empresa && c.SQ_PES_CLI == sqPesCli)
			.Select(c => c.SQ_PES_CRO)
			.Distinct()
			.ToListAsync(cancellationToken);

		if (associado.SQ_PES_CRO_CAD is decimal cadastradoPor && !corretores.Contains(cadastradoPor))
		{
			corretores.Add(cadastradoPor);
		}

		return new Cadastro(pessoa, pessoaFisica, associado, corretores);
	}

	/// <summary>
	/// Confere nas tabelas do sistema: filial (T000PARA), situação (T000SCLI),
	/// município (T000MUNI), tratamento (T000TRTA) e corretor (T009CRRT).
	/// </summary>
	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal empresa,
		CadastroAssociadoDados dados,
		decimal? corretor,
		CancellationToken cancellationToken)
	{
		var pf = dados.PessoaFisica!;
		var associado = dados.Associado!;

		var filial = associado.CdFil!.Value;

		if (!await db.Filiais.AnyAsync(f => f.CD_EMP == empresa && f.CD_FIL == filial, cancellationToken))
		{
			return $"A filial {filial:0} não está cadastrada (T000PARA).";
		}

		var situacao = associado.CdSitCli!.Value;

		if (!await db.SituacoesAssociados.AnyAsync(s => s.CD_EMP == empresa && s.CD_SIT_CLI == situacao, cancellationToken))
		{
			return $"A situação {situacao:0} não está cadastrada (T000SCLI).";
		}

		if (pf.CdMunicipio is decimal municipio
			&& !await db.Municipios.AnyAsync(m => m.CD_EMP == empresa && m.CD_MUN == municipio, cancellationToken))
		{
			return $"O município {municipio:0} não está cadastrado (T000MUNI).";
		}

		if (pf.CdTratamento is decimal tratamento
			&& !await db.Tratamentos.AnyAsync(t => t.CD_EMP == empresa && t.CD_TRT == tratamento, cancellationToken))
		{
			return $"O tratamento {tratamento:0} não está cadastrado (T000TRTA).";
		}

		if (corretor is decimal sqPesCorretor
			&& !await db.Corretores.AnyAsync(c => c.CD_EMP == empresa && c.SQ_PES_CRO == sqPesCorretor, cancellationToken))
		{
			return $"O corretor {sqPesCorretor:0} não está cadastrado (T009CRRT).";
		}

		return null;
	}

	/// <summary>
	/// Copia os dados recebidos para as entidades (inclusão e alteração).
	/// </summary>
	private static void Aplicar(
		CadastroAssociadoDados dados,
		T000PESS pessoa,
		T000PFIS pessoaFisica,
		T000CLIE cliente)
	{
		var pf = dados.PessoaFisica!;
		var associado = dados.Associado!;

		pessoa.NM_PES = dados.Nome!.Trim();
		pessoa.NC_PES = Texto(dados.NomeCurto);
		pessoa.DE_COR_ELE = Texto(dados.Email);
		pessoa.OB_PES = Texto(dados.Observacao);

		pessoaFisica.NR_CPF = NumeroCpf(pf.Cpf);
		pessoaFisica.NR_RG = Texto(pf.Rg);
		pessoaFisica.NM_ORG_EXP = Texto(pf.OrgaoExpedidor);
		pessoaFisica.SG_UF_ORG = Texto(pf.UfOrgao)?.ToUpperInvariant();
		pessoaFisica.DT_NAS = pf.DataNascimento?.Date;
		pessoaFisica.ID_SEX = pf.Sexo!.Trim().ToUpperInvariant();
		pessoaFisica.TP_EST_CIV = Texto(pf.EstadoCivil)?.ToUpperInvariant();
		pessoaFisica.CD_MUN = pf.CdMunicipio;
		pessoaFisica.CD_TRT = pf.CdTratamento;
		pessoaFisica.NM_PAI = Texto(pf.NomePai);
		pessoaFisica.NM_MAE = Texto(pf.NomeMae);
		pessoaFisica.NM_CJG = Texto(pf.NomeConjuge);
		pessoaFisica.DT_NAS_CJG = pf.DataNascimentoConjuge?.Date;
		pessoaFisica.DT_CAS = pf.DataCasamento?.Date;

		cliente.CD_FIL = associado.CdFil;
		cliente.CD_SIT_CLI = associado.CdSitCli;
		cliente.OB_SIT = Texto(associado.ObsSituacao);
		cliente.DT_CARTEIRA = associado.DataCarteira;
	}

	private static AssociadoCadastroDto ParaDto(
		T000PESS pessoa,
		T000PFIS pessoaFisica,
		T000CLIE cliente,
		string? situacao)
	{
		return new AssociadoCadastroDto
		{
			SqPesCli = cliente.SQ_PES_CLI,
			Nome = pessoa.NM_PES,
			NomeCurto = pessoa.NC_PES,
			Email = pessoa.DE_COR_ELE,
			Observacao = pessoa.OB_PES,
			DataCadastro = pessoa.DT_CAD,
			UsuarioCadastro = pessoa.NM_USU,
			Situacao = situacao,
			SqPesCorretorCadastro = cliente.SQ_PES_CRO_CAD,
			PessoaFisica = new PessoaFisicaDados
			{
				Cpf = DocumentoCpf.Formatar(pessoaFisica.NR_CPF),
				Rg = pessoaFisica.NR_RG,
				OrgaoExpedidor = pessoaFisica.NM_ORG_EXP,
				UfOrgao = pessoaFisica.SG_UF_ORG,
				DataNascimento = pessoaFisica.DT_NAS,
				Sexo = pessoaFisica.ID_SEX,
				EstadoCivil = pessoaFisica.TP_EST_CIV,
				CdMunicipio = pessoaFisica.CD_MUN,
				CdTratamento = pessoaFisica.CD_TRT,
				NomePai = pessoaFisica.NM_PAI,
				NomeMae = pessoaFisica.NM_MAE,
				NomeConjuge = pessoaFisica.NM_CJG,
				DataNascimentoConjuge = pessoaFisica.DT_NAS_CJG,
				DataCasamento = pessoaFisica.DT_CAS
			},
			Associado = new AssociadoDados
			{
				CdFil = cliente.CD_FIL,
				CdSitCli = cliente.CD_SIT_CLI,
				ObsSituacao = cliente.OB_SIT,
				DataCarteira = cliente.DT_CARTEIRA
			}
		};
	}

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	/// <summary>
	/// Empresa da base (T009EMPS). Null quando não há exatamente uma.
	/// </summary>
	private static async Task<decimal?> ObterEmpresaAsync(
		SigeraDbContext db,
		CancellationToken cancellationToken)
	{
		var empresas = await db.Empresas
			.AsNoTracking()
			.Select(e => e.CD_EMP)
			.Take(2)
			.ToListAsync(cancellationToken);

		return empresas.Count == 1 ? empresas[0] : null;
	}

	/// <summary>
	/// SQ_PES = MAX + 1, como no sistema legado. UPDLOCK e HOLDLOCK seguram
	/// a leitura até o fim da transação: outro cadastro espera em vez de
	/// pegar o mesmo número.
	/// </summary>
	private static async Task<decimal> ProximoSqPesAsync(
		SigeraDbContext db,
		decimal empresa,
		CancellationToken cancellationToken)
	{
		var resultado = await db.Database
			.SqlQuery<decimal>($"SELECT ISNULL(MAX(SQ_PES), 0) + 1 AS [Value] FROM dbo.T000PESS WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {empresa}")
			.ToListAsync(cancellationToken);

		return resultado.Single();
	}

	private static async Task<string?> DescricaoSituacaoAsync(
		SigeraDbContext db,
		decimal empresa,
		decimal? cdSitCli,
		CancellationToken cancellationToken)
	{
		if (cdSitCli is not decimal situacao)
		{
			return null;
		}

		return await db.SituacoesAssociados
			.AsNoTracking()
			.Where(s => s.CD_EMP == empresa && s.CD_SIT_CLI == situacao)
			.Select(s => s.DE_SIT_CLI)
			.FirstOrDefaultAsync(cancellationToken);
	}

	private static bool ChaveDuplicada(DbUpdateException ex) =>
		ex.InnerException is SqlException { Number: 2627 or 2601 };

	private static decimal NumeroCpf(string? cpf) =>
		decimal.Parse(DocumentoCpf.SomenteDigitos(cpf)!, CultureInfo.InvariantCulture);

	private static string? Texto(string? valor) =>
		string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

	private static string Truncar(string valor, int tamanho) =>
		valor.Length <= tamanho ? valor : valor[..tamanho];

	private static ResultadoCadastroAssociado Invalido(string mensagem) =>
		new(StatusCadastroAssociado.Invalido, mensagem);

	private static ResultadoCadastroAssociado NaoEncontrado() =>
		new(StatusCadastroAssociado.NaoEncontrado, "Associado não encontrado.");

	private static ResultadoCadastroAssociado CpfRepetido(decimal sqPes) =>
		new(StatusCadastroAssociado.Conflito, $"Já existe uma pessoa com este CPF (código {sqPes:0}).");

	private static ResultadoCadastroAssociado ProibidoFalecido() =>
		new(StatusCadastroAssociado.Proibido,
			"Somente Administradores e Gestores podem marcar o associado como falecido " +
			"(isso encerra os contratos ativos).");

	private static ResultadoCadastroAssociado EmpresaIndefinida() =>
		new(StatusCadastroAssociado.Falha,
			"Não foi possível identificar a empresa do sistema: a tabela T009EMPS deve ter exatamente uma empresa.");
}
