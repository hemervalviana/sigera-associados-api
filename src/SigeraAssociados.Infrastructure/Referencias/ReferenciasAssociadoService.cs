using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Referencias;
using SigeraAssociados.Application.Referencias;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Referencias;

/// <summary>
/// Referências pessoais do associado (T000REFE). SQ_REF = MAX + 1 da pessoa
/// (até 9999), com UPDLOCK, HOLDLOCK.
/// </summary>
public sealed class ReferenciasAssociadoService : IReferenciasAssociadoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public ReferenciasAssociadoService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ReferenciaAssociadoDto>?> ListarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return (await db.Referencias
				.AsNoTracking()
				.Where(r => r.CD_EMP == emp && r.SQ_PES == sqPesCli)
				.OrderBy(r => r.SQ_REF)
				.ToListAsync(cancellationToken))
			.Select(ParaDto)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ReferenciaAssociadoDto>> IncluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		ReferenciaAssociadoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoReferenciaAssociado.Validar(dados) is string erro)
		{
			return Invalido<ReferenciaAssociadoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ReferenciaAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		if (await DuplicadaAsync(db, emp, sqPesCli, null, dados, cancellationToken))
		{
			return Conflito<ReferenciaAssociadoDto>("O associado já tem esta referência (mesmo nome e telefone 1).");
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqReferencia = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_REF), 0) + 1 AS [Value] FROM dbo.T000REFE WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND SQ_PES = {sqPesCli}",
			cancellationToken);

		if (sqReferencia > ValidacaoReferenciaAssociado.MaximoReferencias)
		{
			return Invalido<ReferenciaAssociadoDto>("O associado atingiu o limite de 9999 referências.");
		}

		var referencia = new T000REFE { CD_EMP = emp, SQ_PES = sqPesCli, SQ_REF = sqReferencia };
		Aplicar(dados, referencia);
		db.Referencias.Add(referencia);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ReferenciaAssociadoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Referência incluída.", ParaDto(referencia));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ReferenciaAssociadoDto>> AlterarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqReferencia,
		ReferenciaAssociadoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoReferenciaAssociado.Validar(dados) is string erro)
		{
			return Invalido<ReferenciaAssociadoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ReferenciaAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var referencia = await BuscarAsync(db, emp, sqPesCli, sqReferencia, cancellationToken);

		if (referencia is null)
		{
			return NaoEncontrado<ReferenciaAssociadoDto>("Referência não encontrada.");
		}

		if (await DuplicadaAsync(db, emp, sqPesCli, sqReferencia, dados, cancellationToken))
		{
			return Conflito<ReferenciaAssociadoDto>("O associado já tem esta referência (mesmo nome e telefone 1).");
		}

		Aplicar(dados, referencia);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ReferenciaAssociadoDto>(erroGravacao);
		}

		return Sucesso("Referência alterada.", ParaDto(referencia));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ReferenciaAssociadoDto>> ExcluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqReferencia,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ReferenciaAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var referencia = await BuscarAsync(db, emp, sqPesCli, sqReferencia, cancellationToken);

		if (referencia is null)
		{
			return NaoEncontrado<ReferenciaAssociadoDto>("Referência não encontrada.");
		}

		var dto = ParaDto(referencia);
		db.Referencias.Remove(referencia);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ReferenciaAssociadoDto>(erroGravacao)
			: Sucesso("Referência excluída.", dto);
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

	private static Task<T000REFE?> BuscarAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal sqReferencia,
		CancellationToken cancellationToken) =>
		db.Referencias.FirstOrDefaultAsync(
			r => r.CD_EMP == emp && r.SQ_PES == sqPesCli && r.SQ_REF == sqReferencia,
			cancellationToken);

	/// <summary>
	/// Mesmo nome (sem diferenciar maiúsculas) e mesmo telefone 1 em outra
	/// referência do associado. O telefone é comparado só pelos dígitos.
	/// </summary>
	private static async Task<bool> DuplicadaAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? sqReferencia,
		ReferenciaAssociadoDados dados,
		CancellationToken cancellationToken)
	{
		var nome = dados.Nome!.Trim();
		var telefone = ValidacaoReferenciaAssociado.Normalizar(dados.Telefone1);

		var outras = await db.Referencias
			.AsNoTracking()
			.Where(r => r.CD_EMP == emp && r.SQ_PES == sqPesCli)
			.Where(r => sqReferencia == null || r.SQ_REF != sqReferencia)
			.Select(r => new { r.NM_REF, r.NR_FN1_REF })
			.ToListAsync(cancellationToken);

		return outras.Any(r =>
			string.Equals(r.NM_REF.Trim(), nome, StringComparison.OrdinalIgnoreCase)
			&& ValidacaoReferenciaAssociado.Normalizar(r.NR_FN1_REF) == telefone);
	}

	private static void Aplicar(ReferenciaAssociadoDados d, T000REFE r)
	{
		r.NM_REF = d.Nome!.Trim();
		r.DT_NAS_REF = d.DataNascimento?.Date;
		r.NR_FN1_REF = ValidacaoReferenciaAssociado.Normalizar(d.Telefone1)!;
		r.NR_FN2_REF = ValidacaoReferenciaAssociado.Normalizar(d.Telefone2);
	}

	private static ReferenciaAssociadoDto ParaDto(T000REFE r) => new()
	{
		SqReferencia = r.SQ_REF,
		Nome = r.NM_REF.Trim(),
		DataNascimento = r.DT_NAS_REF,
		Telefone1 = r.NR_FN1_REF.Trim(),
		Telefone1Formatado = ValidacaoReferenciaAssociado.Formatar(r.NR_FN1_REF) ?? string.Empty,
		Telefone2 = string.IsNullOrWhiteSpace(r.NR_FN2_REF) ? null : r.NR_FN2_REF.Trim(),
		Telefone2Formatado = ValidacaoReferenciaAssociado.Formatar(r.NR_FN2_REF)
	};
}
