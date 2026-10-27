using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Corretores;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Corretores;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Pessoas;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Corretores;

/// <summary>
/// Grava corretores nas tabelas do legado: T000PESS (pessoa), T000PFIS ou
/// T000PJUR (física ou jurídica, nunca as duas) e T009CRRT (corretor).
///
/// - Inclusão numa transação, com SQ_PES = MAX + 1 lido com UPDLOCK, HOLDLOCK.
///   CPF/CNPJ de uma pessoa que ainda não é corretor (por exemplo, um
///   associado): a pessoa é aproveitada e só o T009CRRT é criado; os dados
///   pessoais já gravados não mudam, só os campos vazios são completados.
/// - Alteração grava só as colunas que mudaram; o tipo de pessoa não muda.
/// - Exclusão lógica em T009CRRT (DT_EXC_API, NM_USU_EXC_API), como a do associado.
/// </summary>
public sealed class CadastroCorretoresService : ICadastroCorretoresService
{
	private const int TamanhoPaginaMaximo = 200;
	private const int TentativasNumeracao = 3;

	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;

	public CadastroCorretoresService(SigeraDbContextFactory factory, SigeraSystemContext sistema)
	{
		_factory = factory;
		_sistema = sistema;
	}

	// ===================== Pesquisa =====================

	/// <summary>Linha da pesquisa, antes de formatar o documento.</summary>
	private sealed class LinhaCorretor
	{
		public decimal Sq { get; init; }
		public string Nome { get; init; } = string.Empty;
		public string? NomeCurto { get; init; }
		public string Tipo { get; init; } = string.Empty;
		public decimal? Cpf { get; init; }
		public decimal? Cnpj { get; init; }
		public decimal? CdFil { get; init; }
		public decimal? CdSituacao { get; init; }
		public string? Classe { get; init; }
		public int? TipoCorretor { get; init; }
	}

	/// <inheritdoc />
	public async Task<PesquisaCorretoresResultado> PesquisarAsync(
		PesquisaCorretoresRequest request,
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

		string? tipo = null;

		if (!string.IsNullOrWhiteSpace(request.TipoPessoa)
			&& (tipo = ValidacaoCadastroCorretor.NormalizarTipo(request.TipoPessoa)) is null)
		{
			throw new DadosInvalidosException("O tipo de pessoa deve ser F (física) ou J (jurídica).");
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			throw new DadosInvalidosException("Não foi possível identificar a empresa do sistema: a tabela T009EMPS deve ter exatamente uma empresa.");
		}

		// Só corretores não excluídos e coerentes com o tipo: física com
		// T000PFIS e sem T000PJUR; jurídica com T000PJUR e sem T000PFIS.
		var query =
			from c in db.Corretores.AsNoTracking()
			where c.CD_EMP == emp && c.DT_EXC_API == null
			join p in db.Pessoas.AsNoTracking()
				on new { Empresa = c.CD_EMP, Pessoa = c.SQ_PES_CRO }
				equals new { Empresa = p.CD_EMP, Pessoa = p.SQ_PES }
			let temFisica = db.PessoasFisicas.Any(f => f.CD_EMP == p.CD_EMP && f.SQ_PES_FIS == p.SQ_PES)
			let temJuridica = db.PessoasJuridicas.Any(j => j.CD_EMP == p.CD_EMP && j.SQ_PES_JUR == p.SQ_PES)
			where (p.TP_PES == ValidacaoCadastroCorretor.PessoaFisica && temFisica && !temJuridica)
				|| (p.TP_PES == ValidacaoCadastroCorretor.PessoaJuridica && temJuridica && !temFisica)
			select new LinhaCorretor
			{
				Sq = c.SQ_PES_CRO,
				Nome = p.NM_PES,
				NomeCurto = p.NC_PES,
				Tipo = p.TP_PES,
				Cpf = db.PessoasFisicas.Where(f => f.CD_EMP == p.CD_EMP && f.SQ_PES_FIS == p.SQ_PES).Select(f => f.NR_CPF).FirstOrDefault(),
				Cnpj = db.PessoasJuridicas.Where(j => j.CD_EMP == p.CD_EMP && j.SQ_PES_JUR == p.SQ_PES).Select(j => j.NR_CPJ).FirstOrDefault(),
				CdFil = c.CD_FIL,
				CdSituacao = c.CD_SIT_CRRT,
				Classe = c.CL_CRO,
				TipoCorretor = c.TP_CRRT
			};

		if (request.CdFil is decimal filial)
		{
			query = query.Where(c => c.CdFil == filial);
		}

		if (tipo is not null)
		{
			query = query.Where(c => c.Tipo == tipo);
		}

		if (request.CdSituacao is decimal situacao)
		{
			query = query.Where(c => c.CdSituacao == situacao);
		}

		if (!string.IsNullOrWhiteSpace(request.Busca))
		{
			query = Buscar(query, request.Busca.Trim());
		}

		var total = await query.CountAsync(cancellationToken);

		var linhas = await query
			.OrderBy(c => c.Nome)
			.ThenBy(c => c.Sq)
			.Skip((request.Pagina - 1) * request.TamanhoPagina)
			.Take(request.TamanhoPagina)
			.ToListAsync(cancellationToken);

		return new PesquisaCorretoresResultado
		{
			Pagina = request.Pagina,
			TamanhoPagina = request.TamanhoPagina,
			TotalRegistros = total,
			TotalPaginas = (int)Math.Ceiling(total / (double)request.TamanhoPagina),
			Corretores = linhas.Select(l => new CorretorResumoDto
			{
				SqPesCorretor = l.Sq,
				Nome = l.Nome,
				NomeCurto = l.NomeCurto,
				TipoPessoa = l.Tipo,
				Documento = l.Tipo == ValidacaoCadastroCorretor.PessoaFisica
					? DocumentoCpf.Formatar(l.Cpf)
					: DocumentoCnpj.Formatar(l.Cnpj),
				CdFil = l.CdFil,
				CdSituacao = l.CdSituacao,
				Classe = l.Classe,
				Tipo = l.TipoCorretor
			}).ToList()
		};
	}

	/// <summary>
	/// CPF (11 números) ou CNPJ (14) com ou sem pontuação: documento exato.
	/// Outro número: código exato ou começo do nome. Texto: começo do nome.
	/// </summary>
	private static IQueryable<LinhaCorretor> Buscar(IQueryable<LinhaCorretor> query, string busca)
	{
		var soDocumento = busca.All(ch => char.IsAsciiDigit(ch) || ch is '.' or '-' or '/' or ' ');
		var digitos = DocumentoCnpj.SomenteDigitos(busca);

		if (soDocumento && digitos is { Length: 11 } && DocumentoCpf.Valido(digitos))
		{
			var cpf = decimal.Parse(digitos, CultureInfo.InvariantCulture);
			return query.Where(c => c.Cpf == cpf);
		}

		if (soDocumento && digitos is { Length: 14 } && DocumentoCnpj.Valido(digitos))
		{
			var cnpj = decimal.Parse(digitos, CultureInfo.InvariantCulture);
			return query.Where(c => c.Cnpj == cnpj);
		}

		return decimal.TryParse(busca, NumberStyles.None, CultureInfo.InvariantCulture, out var codigo)
			? query.Where(c => c.Sq == codigo || c.Nome.StartsWith(busca))
			: query.Where(c => c.Nome.StartsWith(busca));
	}

	// ===================== Detalhe =====================

	/// <inheritdoc />
	public async Task<CorretorCadastroDto?> ObterAsync(
		decimal sqPesCorretor,
		bool incluirSenha,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return null;
		}

		var cadastro = await CarregarAsync(db, emp, sqPesCorretor, rastrear: false, cancellationToken);

		return cadastro is null
			? null
			: ParaDto(cadastro, await NomeRepresentanteAsync(db, emp, cadastro.Corretor.SQ_PES_RPR, cancellationToken), incluirSenha,
				await DescricaoCargoAsync(db, emp, cadastro.Juridica, cancellationToken));
	}

	// ===================== Inclusão =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CorretorCadastroDto>> IncluirAsync(
		CadastroCorretorDados dados,
		string login,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoCadastroCorretor.Validar(dados) is string erro)
		{
			return Invalido<CorretorCadastroDto>(erro);
		}

		var tipo = ValidacaoCadastroCorretor.NormalizarTipo(dados.TipoPessoa)!;

		for (var tentativa = 1; ; tentativa++)
		{
			await using var db = CriarContexto();

			if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
			{
				return EmpresaIndefinida<CorretorCadastroDto>();
			}

			// Pessoa já cadastrada com o documento (por exemplo, um associado): aproveita.
			if (await DonoDocumentoAsync(db, emp, dados, tipo, sqProprio: null, cancellationToken) is decimal existente)
			{
				return await IncluirComPessoaExistenteAsync(db, emp, existente, dados, tipo, cancellationToken);
			}

			if (await ValidarReferenciasAsync(db, emp, dados, sqProprio: null, cancellationToken) is string erroReferencia)
			{
				return Invalido<CorretorCadastroDto>(erroReferencia);
			}

			await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

			try
			{
				var sqPes = await NumeroAsync(db,
					$"SELECT ISNULL(MAX(SQ_PES), 0) + 1 AS [Value] FROM dbo.T000PESS WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
					cancellationToken);

				var pessoa = new T000PESS
				{
					CD_EMP = emp,
					SQ_PES = sqPes,
					TP_PES = tipo,
					DT_CAD = DateTime.Now,
					NM_USU = Truncar(login, 20)
				};

				AplicarPessoa(dados, pessoa);

				// T000PESS primeiro: as outras três têm FK para ela.
				db.Pessoas.Add(pessoa);
				await db.SaveChangesAsync(cancellationToken);

				T000PFIS? fisica = null;
				T000PJUR? juridica = null;

				if (tipo == ValidacaoCadastroCorretor.PessoaFisica)
				{
					fisica = new T000PFIS { CD_EMP = emp, SQ_PES_FIS = sqPes };
					AplicarFisica(dados.PessoaFisica!, fisica);
					db.PessoasFisicas.Add(fisica);
				}
				else
				{
					juridica = new T000PJUR { CD_EMP = emp, SQ_PES_JUR = sqPes };
					AplicarJuridica(dados.PessoaJuridica!, juridica);
					db.PessoasJuridicas.Add(juridica);
				}

				var corretor = new T009CRRT { CD_EMP = emp, SQ_PES_CRO = sqPes };
				AplicarCorretor(dados.Corretor ?? new DadosCorretor(), corretor);
				db.Corretores.Add(corretor);

				await db.SaveChangesAsync(cancellationToken);
				await transacao.CommitAsync(cancellationToken);

				var cadastro = new Cadastro(pessoa, fisica, juridica, corretor);

				return Sucesso("Corretor cadastrado.",
					ParaDto(cadastro, await NomeRepresentanteAsync(db, emp, corretor.SQ_PES_RPR, cancellationToken), incluirSenha: true,
						await DescricaoCargoAsync(db, emp, juridica, cancellationToken)));
			}
			catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 } && tentativa < TentativasNumeracao)
			{
				// Outro cadastro pegou o mesmo SQ_PES: desfaz e tenta com o próximo.
				await transacao.RollbackAsync(cancellationToken);
			}
			catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
			{
				await transacao.RollbackAsync(cancellationToken);
				return Invalido<CorretorCadastroDto>("Um dos códigos informados não existe na tabela de referência do sistema.");
			}
		}
	}

	// ===================== Alteração =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CorretorCadastroDto>> AlterarAsync(
		decimal sqPesCorretor,
		CadastroCorretorDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoCadastroCorretor.Validar(dados) is string erro)
		{
			return Invalido<CorretorCadastroDto>(erro);
		}

		var tipo = ValidacaoCadastroCorretor.NormalizarTipo(dados.TipoPessoa)!;

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<CorretorCadastroDto>();
		}

		var cadastro = await CarregarAsync(db, emp, sqPesCorretor, rastrear: true, cancellationToken);

		if (cadastro is null)
		{
			return NaoEncontrado<CorretorCadastroDto>("Corretor não encontrado.");
		}

		if (tipo != cadastro.Pessoa.TP_PES)
		{
			return Invalido<CorretorCadastroDto>(
				$"O tipo de pessoa do corretor não pode ser alterado (ele é pessoa {(cadastro.Pessoa.TP_PES == ValidacaoCadastroCorretor.PessoaFisica ? "física" : "jurídica")}).");
		}

		if (await ValidarReferenciasAsync(db, emp, dados, sqPesCorretor, cancellationToken) is string erroReferencia)
		{
			return Invalido<CorretorCadastroDto>(erroReferencia);
		}

		if (await DonoDocumentoAsync(db, emp, dados, tipo, sqPesCorretor, cancellationToken) is decimal dono)
		{
			return Conflito<CorretorCadastroDto>(
				$"Já existe uma pessoa com este {(tipo == ValidacaoCadastroCorretor.PessoaFisica ? "CPF" : "CNPJ")} (código {dono:0}).");
		}

		AplicarPessoa(dados, cadastro.Pessoa);

		if (cadastro.Fisica is not null)
		{
			AplicarFisica(dados.PessoaFisica!, cadastro.Fisica);
		}
		else
		{
			AplicarJuridica(dados.PessoaJuridica!, cadastro.Juridica!);
		}

		// Bloco do corretor omitido: T009CRRT fica como está.
		if (dados.Corretor is not null)
		{
			AplicarCorretor(dados.Corretor, cadastro.Corretor);
		}

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<CorretorCadastroDto>(erroGravacao);
		}

		return Sucesso("Corretor alterado.",
			ParaDto(cadastro, await NomeRepresentanteAsync(db, emp, cadastro.Corretor.SQ_PES_RPR, cancellationToken), incluirSenha: true,
				await DescricaoCargoAsync(db, emp, cadastro.Juridica, cancellationToken)));
	}

	// ===================== Exclusão =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CorretorCadastroDto>> ExcluirAsync(
		decimal sqPesCorretor,
		string login,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<CorretorCadastroDto>();
		}

		var cadastro = await CarregarAsync(db, emp, sqPesCorretor, rastrear: true, cancellationToken);

		if (cadastro is null)
		{
			return NaoEncontrado<CorretorCadastroDto>("Corretor não encontrado.");
		}

		// Exclusão lógica: nada é apagado; o legado continua mostrando.
		cadastro.Corretor.DT_EXC_API = DateTime.Now;
		cadastro.Corretor.NM_USU_EXC_API = Truncar(login, 256);

		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Corretor excluído.", ParaDto(cadastro, null, incluirSenha: false));
	}

	// ===================== Apoio =====================

	private sealed record Cadastro(T000PESS Pessoa, T000PFIS? Fisica, T000PJUR? Juridica, T009CRRT Corretor);

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	/// <summary>
	/// Corretor não excluído, com a pessoa e o complemento do tipo dele.
	/// Fora da regra (física sem T000PFIS, jurídica sem T000PJUR, ou com as
	/// duas): null, como se não existisse.
	/// </summary>
	private static async Task<Cadastro?> CarregarAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sq,
		bool rastrear,
		CancellationToken cancellationToken)
	{
		var corretores = rastrear ? db.Corretores : db.Corretores.AsNoTracking();
		var pessoas = rastrear ? db.Pessoas : db.Pessoas.AsNoTracking();
		var fisicas = rastrear ? db.PessoasFisicas : db.PessoasFisicas.AsNoTracking();
		var juridicas = rastrear ? db.PessoasJuridicas : db.PessoasJuridicas.AsNoTracking();

		var corretor = await corretores.SingleOrDefaultAsync(
			c => c.CD_EMP == emp && c.SQ_PES_CRO == sq && c.DT_EXC_API == null, cancellationToken);

		if (corretor is null)
		{
			return null;
		}

		var pessoa = await pessoas.SingleOrDefaultAsync(p => p.CD_EMP == emp && p.SQ_PES == sq, cancellationToken);
		var fisica = await fisicas.SingleOrDefaultAsync(f => f.CD_EMP == emp && f.SQ_PES_FIS == sq, cancellationToken);
		var juridica = await juridicas.SingleOrDefaultAsync(j => j.CD_EMP == emp && j.SQ_PES_JUR == sq, cancellationToken);

		var coerente = pessoa?.TP_PES switch
		{
			ValidacaoCadastroCorretor.PessoaFisica => fisica is not null && juridica is null,
			ValidacaoCadastroCorretor.PessoaJuridica => juridica is not null && fisica is null,
			_ => false
		};

		return coerente ? new Cadastro(pessoa!, fisica, juridica, corretor) : null;
	}

	/// <summary>
	/// Confere filial, municípios, tratamento, representante (outro corretor
	/// não excluído), coordenador (T009COOR) e agente (T009AGENT).
	/// </summary>
	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		CadastroCorretorDados dados,
		decimal? sqProprio,
		CancellationToken cancellationToken)
	{
		if (dados.PessoaFisica is { } pf)
		{
			if (pf.CdMunicipio is decimal munPf
				&& !await db.Municipios.AnyAsync(m => m.CD_EMP == emp && m.CD_MUN == munPf, cancellationToken))
			{
				return $"O município {munPf:0} não está cadastrado (T000MUNI).";
			}

			if (pf.CdTratamento is decimal tratamento
				&& !await db.Tratamentos.AnyAsync(t => t.CD_EMP == emp && t.CD_TRT == tratamento, cancellationToken))
			{
				return $"O tratamento {tratamento:0} não está cadastrado (T000TRTA).";
			}
		}

		if (dados.PessoaJuridica?.CdCargo is decimal cargo
			&& !await db.Cargos.AnyAsync(g => g.CD_EMP == emp && g.CD_CG == cargo, cancellationToken))
		{
			return $"O cargo {cargo:0} não está cadastrado (T000CARG).";
		}

		if (dados.Corretor is not { } c)
		{
			return null;
		}

		if (c.CdFil is decimal filial
			&& !await db.Filiais.AnyAsync(f => f.CD_EMP == emp && f.CD_FIL == filial, cancellationToken))
		{
			return $"A filial {filial:0} não está cadastrada (T000PARA).";
		}

		if (c.CdMunicipio is decimal municipio
			&& !await db.Municipios.AnyAsync(m => m.CD_EMP == emp && m.CD_MUN == municipio, cancellationToken))
		{
			return $"O município {municipio:0} não está cadastrado (T000MUNI).";
		}

		if (c.SqPesRepresentante is decimal representante)
		{
			if (representante == sqProprio)
			{
				return "O corretor não pode ser representante de si mesmo.";
			}

			if (!await db.Corretores.AnyAsync(
					r => r.CD_EMP == emp && r.SQ_PES_CRO == representante && r.DT_EXC_API == null, cancellationToken))
			{
				return $"O representante {representante:0} não é um corretor ativo deste sistema.";
			}
		}

		if (c.SqCoordenador is decimal coordenador
			&& await NumeroAsync(db,
				$"SELECT CAST(COUNT(*) AS decimal(10, 0)) AS [Value] FROM dbo.T009COOR WHERE CD_EMP = {emp} AND SQ_CDR = {coordenador}",
				cancellationToken) == 0)
		{
			return $"O coordenador {coordenador:0} não está cadastrado (T009COOR).";
		}

		if (c.SqAgente is decimal agente
			&& await NumeroAsync(db,
				$"SELECT CAST(COUNT(*) AS decimal(10, 0)) AS [Value] FROM dbo.T009AGENT WHERE CD_EMP = {emp} AND SQ_AGEN = {agente}",
				cancellationToken) == 0)
		{
			return $"O agente {agente:0} não está cadastrado (T009AGENT).";
		}

		return null;
	}

	/// <summary>
	/// Pessoa (outra que não sqProprio) que já tem o CPF (física) ou o CNPJ
	/// (jurídica) informado; null quando ninguém tem.
	/// </summary>
	private static async Task<decimal?> DonoDocumentoAsync(
		SigeraDbContext db,
		decimal emp,
		CadastroCorretorDados dados,
		string tipo,
		decimal? sqProprio,
		CancellationToken cancellationToken)
	{
		if (tipo == ValidacaoCadastroCorretor.PessoaFisica)
		{
			var cpf = Numero(DocumentoCpf.SomenteDigitos(dados.PessoaFisica!.Cpf));

			return await db.PessoasFisicas.AsNoTracking()
				.Where(f => f.CD_EMP == emp && f.NR_CPF == cpf && f.SQ_PES_FIS != sqProprio)
				.Select(f => (decimal?)f.SQ_PES_FIS)
				.FirstOrDefaultAsync(cancellationToken);
		}

		var cnpj = Numero(DocumentoCnpj.SomenteDigitos(dados.PessoaJuridica!.Cnpj));

		return await db.PessoasJuridicas.AsNoTracking()
			.Where(j => j.CD_EMP == emp && j.NR_CPJ == cnpj && j.SQ_PES_JUR != sqProprio)
			.Select(j => (decimal?)j.SQ_PES_JUR)
			.FirstOrDefaultAsync(cancellationToken);
	}

	/// <summary>
	/// O documento já é de uma pessoa: se ela ainda não é corretor e o
	/// cadastro é coerente com o tipo, cria só o T009CRRT e completa os campos
	/// vazios da pessoa. Já corretor (ou excluído pela API): Conflito.
	/// </summary>
	private static async Task<ResultadoTabelaApoio<CorretorCadastroDto>> IncluirComPessoaExistenteAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPes,
		CadastroCorretorDados dados,
		string tipo,
		CancellationToken cancellationToken)
	{
		var situacao = await PessoaExistente.CarregarAsync(db, emp, sqPes, cancellationToken);

		if (RegrasPessoaExistente.PodeVirarCorretor(situacao, tipo) is string recusa)
		{
			return Conflito<CorretorCadastroDto>(recusa);
		}

		if (await ValidarReferenciasAsync(db, emp, dados, sqPes, cancellationToken) is string erroReferencia)
		{
			return Invalido<CorretorCadastroDto>(erroReferencia);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var pessoa = await db.Pessoas.SingleAsync(p => p.CD_EMP == emp && p.SQ_PES == sqPes, cancellationToken);
		PessoaExistente.ComplementarPessoa(pessoa, dados.NomeCurto, dados.Email, dados.Url, dados.Observacao);

		T000PFIS? fisica = null;
		T000PJUR? juridica = null;

		if (tipo == ValidacaoCadastroCorretor.PessoaFisica)
		{
			fisica = await db.PessoasFisicas.SingleAsync(f => f.CD_EMP == emp && f.SQ_PES_FIS == sqPes, cancellationToken);
			PessoaExistente.ComplementarFisica(fisica, dados.PessoaFisica!);
		}
		else
		{
			juridica = await db.PessoasJuridicas.SingleAsync(j => j.CD_EMP == emp && j.SQ_PES_JUR == sqPes, cancellationToken);
			PessoaExistente.ComplementarJuridica(juridica, dados.PessoaJuridica!);
		}

		var corretor = new T009CRRT { CD_EMP = emp, SQ_PES_CRO = sqPes };
		AplicarCorretor(dados.Corretor ?? new DadosCorretor(), corretor);
		db.Corretores.Add(corretor);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<CorretorCadastroDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso(
			$"Corretor cadastrado aproveitando a pessoa já existente (código {sqPes:0}): " +
			"os dados pessoais já gravados foram mantidos e só os campos vazios foram completados.",
			ParaDto(new Cadastro(pessoa, fisica, juridica, corretor),
				await NomeRepresentanteAsync(db, emp, corretor.SQ_PES_RPR, cancellationToken),
				incluirSenha: true,
				await DescricaoCargoAsync(db, emp, juridica, cancellationToken)));
	}

	private static async Task<string?> DescricaoCargoAsync(
		SigeraDbContext db,
		decimal emp,
		T000PJUR? juridica,
		CancellationToken cancellationToken)
	{
		if (juridica?.CD_CG is not decimal cargo)
		{
			return null;
		}

		return await db.Cargos.AsNoTracking()
			.Where(g => g.CD_EMP == emp && g.CD_CG == cargo)
			.Select(g => g.DE_CG)
			.FirstOrDefaultAsync(cancellationToken);
	}

	private static async Task<string?> NomeRepresentanteAsync(
		SigeraDbContext db,
		decimal emp,
		decimal? representante,
		CancellationToken cancellationToken)
	{
		if (representante is not decimal sq)
		{
			return null;
		}

		return await db.Pessoas.AsNoTracking()
			.Where(p => p.CD_EMP == emp && p.SQ_PES == sq)
			.Select(p => p.NM_PES)
			.FirstOrDefaultAsync(cancellationToken);
	}

	private static void AplicarPessoa(CadastroCorretorDados d, T000PESS p)
	{
		p.NM_PES = d.Nome!.Trim();
		p.NC_PES = Texto(d.NomeCurto);
		p.DE_COR_ELE = Texto(d.Email);
		p.DE_URL = Texto(d.Url);
		p.OB_PES = Texto(d.Observacao);
	}

	private static void AplicarFisica(PessoaFisicaDados pf, T000PFIS f)
	{
		f.NR_CPF = Numero(DocumentoCpf.SomenteDigitos(pf.Cpf));
		f.NR_RG = Texto(pf.Rg);
		f.NM_ORG_EXP = Texto(pf.OrgaoExpedidor);
		f.SG_UF_ORG = Texto(pf.UfOrgao)?.ToUpperInvariant();
		f.DT_NAS = pf.DataNascimento?.Date;
		f.ID_SEX = Texto(pf.Sexo)?.ToUpperInvariant();
		f.TP_EST_CIV = Texto(pf.EstadoCivil)?.ToUpperInvariant();
		f.CD_MUN = pf.CdMunicipio;
		f.CD_TRT = pf.CdTratamento;
		f.NM_PAI = Texto(pf.NomePai);
		f.NM_MAE = Texto(pf.NomeMae);
		f.NM_CJG = Texto(pf.NomeConjuge);
		f.DT_NAS_CJG = pf.DataNascimentoConjuge?.Date;
		f.DT_CAS = pf.DataCasamento?.Date;
	}

	private static void AplicarJuridica(PessoaJuridicaDados pj, T000PJUR j)
	{
		j.NR_CPJ = Numero(DocumentoCnpj.SomenteDigitos(pj.Cnpj));
		j.NR_CGF = pj.InscricaoEstadual;
		j.NM_CON = Texto(pj.Contato);
		j.CD_CG = pj.CdCargo;
	}

	private static void AplicarCorretor(DadosCorretor c, T009CRRT r)
	{
		r.CD_FIL = c.CdFil;
		r.CD_MUN = c.CdMunicipio;
		r.CD_SIT_CRRT = c.CdSituacao;
		r.CL_CRO = Texto(c.Classe);
		r.TP_CRRT = c.Tipo;
		r.NR_ISC_SUS = c.InscricaoSusep;
		r.PC_CMS_ADT = c.ComissaoAdesao;
		r.PC_CMS_CAR = c.ComissaoCarencia;
		r.PC_CMS_PLN = c.ComissaoPlano;
		r.SQ_PES_RPR = c.SqPesRepresentante;
		r.SQ_CDR = c.SqCoordenador;
		r.SQ_AGEN = c.SqAgente;
		r.SQ_GVED = c.SqGved;

		// Senha em texto puro, como o legado lê. Omitida (null) mantém; vazia apaga.
		if (c.SenhaRepresentante is not null)
		{
			r.SH_RPR = c.SenhaRepresentante.Length == 0 ? null : c.SenhaRepresentante;
		}
	}

	private static CorretorCadastroDto ParaDto(Cadastro c, string? nomeRepresentante, bool incluirSenha, string? cargo = null) => new()
	{
		SqPesCorretor = c.Corretor.SQ_PES_CRO,
		TipoPessoa = c.Pessoa.TP_PES,
		Nome = c.Pessoa.NM_PES,
		NomeCurto = c.Pessoa.NC_PES,
		Email = c.Pessoa.DE_COR_ELE,
		Url = c.Pessoa.DE_URL,
		Observacao = c.Pessoa.OB_PES,
		DataCadastro = c.Pessoa.DT_CAD,
		UsuarioCadastro = c.Pessoa.NM_USU,
		PessoaFisica = c.Fisica is not { } f ? null : new PessoaFisicaDados
		{
			Cpf = DocumentoCpf.Formatar(f.NR_CPF),
			Rg = f.NR_RG,
			OrgaoExpedidor = f.NM_ORG_EXP,
			UfOrgao = f.SG_UF_ORG,
			DataNascimento = f.DT_NAS,
			Sexo = f.ID_SEX,
			EstadoCivil = f.TP_EST_CIV,
			CdMunicipio = f.CD_MUN,
			CdTratamento = f.CD_TRT,
			NomePai = f.NM_PAI,
			NomeMae = f.NM_MAE,
			NomeConjuge = f.NM_CJG,
			DataNascimentoConjuge = f.DT_NAS_CJG,
			DataCasamento = f.DT_CAS
		},
		PessoaJuridica = c.Juridica is not { } j ? null : new PessoaJuridicaDados
		{
			Cnpj = DocumentoCnpj.Formatar(j.NR_CPJ),
			InscricaoEstadual = j.NR_CGF,
			Contato = j.NM_CON,
			CdCargo = j.CD_CG,
			Cargo = cargo
		},
		Corretor = new DadosCorretor
		{
			CdFil = c.Corretor.CD_FIL,
			CdMunicipio = c.Corretor.CD_MUN,
			CdSituacao = c.Corretor.CD_SIT_CRRT,
			Classe = c.Corretor.CL_CRO,
			Tipo = c.Corretor.TP_CRRT,
			InscricaoSusep = c.Corretor.NR_ISC_SUS,
			ComissaoAdesao = c.Corretor.PC_CMS_ADT,
			ComissaoCarencia = c.Corretor.PC_CMS_CAR,
			ComissaoPlano = c.Corretor.PC_CMS_PLN,
			SqPesRepresentante = c.Corretor.SQ_PES_RPR,
			SenhaRepresentante = incluirSenha ? c.Corretor.SH_RPR : null,
			SqCoordenador = c.Corretor.SQ_CDR,
			SqAgente = c.Corretor.SQ_AGEN,
			SqGved = c.Corretor.SQ_GVED
		},
		NomeRepresentante = nomeRepresentante
	};

	private static decimal? Numero(string? digitos) =>
		digitos is null ? null : decimal.Parse(digitos, CultureInfo.InvariantCulture);

	private static string Truncar(string valor, int tamanho) =>
		valor.Length <= tamanho ? valor : valor[..tamanho];
}
