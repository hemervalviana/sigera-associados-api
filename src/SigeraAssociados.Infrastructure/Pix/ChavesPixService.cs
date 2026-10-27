using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Pix;
using SigeraAssociados.Application.Pix;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Pix;

/// <summary>
/// Chaves Pix do associado (T009PIX). SQ_REG = MAX + 1 da pessoa (até 99),
/// com UPDLOCK, HOLDLOCK.
/// </summary>
public sealed class ChavesPixService : IChavesPixService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public ChavesPixService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ChavePixDto>?> ListarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return (await Consultar(db, emp, sqPesCli).ToListAsync(cancellationToken))
			.Select(ParaDto)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ChavePixDto>> IncluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		ChavePixDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoChavePix.Validar(dados) is string erro)
		{
			return Invalido<ChavePixDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ChavePixDto>(AcessoTitular.NaoEncontrado(titular));
		}

		if (await DuplicadaAsync(db, emp, sqPesCli, null, dados, cancellationToken))
		{
			return Conflito<ChavePixDto>("O associado já tem esta chave Pix.");
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqPix = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_REG), 0) + 1 AS [Value] FROM dbo.T009PIX WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND SQ_PES = {sqPesCli}",
			cancellationToken);

		if (sqPix > ValidacaoChavePix.MaximoChaves)
		{
			return Invalido<ChavePixDto>("O associado atingiu o limite de 99 chaves Pix.");
		}

		var chave = new T009PIX { CD_EMP = emp, SQ_PES = sqPesCli, SQ_REG = sqPix };
		Aplicar(dados, chave);
		db.ChavesPix.Add(chave);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ChavePixDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Chave Pix incluída.", ParaDto(chave));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ChavePixDto>> AlterarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqPix,
		ChavePixDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoChavePix.Validar(dados) is string erro)
		{
			return Invalido<ChavePixDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ChavePixDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var chave = await db.ChavesPix.FirstOrDefaultAsync(
			p => p.CD_EMP == emp && p.SQ_PES == sqPesCli && p.SQ_REG == sqPix,
			cancellationToken);

		if (chave is null)
		{
			return NaoEncontrado<ChavePixDto>("Chave Pix não encontrada.");
		}

		if (await DuplicadaAsync(db, emp, sqPesCli, sqPix, dados, cancellationToken))
		{
			return Conflito<ChavePixDto>("O associado já tem esta chave Pix.");
		}

		Aplicar(dados, chave);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<ChavePixDto>(erroGravacao);
		}

		return Sucesso("Chave Pix alterada.", ParaDto(chave));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ChavePixDto>> ExcluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqPix,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<ChavePixDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var chave = await db.ChavesPix.FirstOrDefaultAsync(
			p => p.CD_EMP == emp && p.SQ_PES == sqPesCli && p.SQ_REG == sqPix,
			cancellationToken);

		if (chave is null)
		{
			return NaoEncontrado<ChavePixDto>("Chave Pix não encontrada.");
		}

		var dto = ParaDto(chave);
		db.ChavesPix.Remove(chave);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ChavePixDto>(erroGravacao)
			: Sucesso("Chave Pix excluída.", dto);
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

	private static IQueryable<T009PIX> Consultar(SigeraDbContext db, decimal emp, decimal sqPesCli) =>
		db.ChavesPix
			.AsNoTracking()
			.Where(p => p.CD_EMP == emp && p.SQ_PES == sqPesCli)
			.OrderBy(p => p.SQ_REG);

	/// <summary>
	/// Mesma chave (do mesmo tipo) em outro registro do associado. Compara na
	/// forma normalizada, porque o legado pode ter gravado com pontuação.
	/// </summary>
	private static async Task<bool> DuplicadaAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? sqPix,
		ChavePixDados dados,
		CancellationToken cancellationToken)
	{
		var tipo = dados.CdTipo!.Value;
		var chave = ValidacaoChavePix.Normalizar(tipo, dados.Chave!);

		var chaves = await db.ChavesPix
			.AsNoTracking()
			.Where(p => p.CD_EMP == emp && p.SQ_PES == sqPesCli && p.CD_PIX == tipo)
			.Where(p => sqPix == null || p.SQ_REG != sqPix)
			.Select(p => p.VL_CHAVE)
			.ToListAsync(cancellationToken);

		return chaves.Any(c => ValidacaoChavePix.Normalizar(tipo, c) == chave);
	}

	private static void Aplicar(ChavePixDados d, T009PIX p)
	{
		p.CD_PIX = d.CdTipo!.Value;
		p.VL_CHAVE = ValidacaoChavePix.Normalizar(p.CD_PIX, d.Chave!);
	}

	private static ChavePixDto ParaDto(T009PIX p) => new()
	{
		SqPix = p.SQ_REG,
		CdTipo = p.CD_PIX,
		Tipo = TipoChavePix.Descricao(p.CD_PIX),
		Chave = p.VL_CHAVE.Trim(),
		Formatada = ValidacaoChavePix.Formatar(p.CD_PIX, p.VL_CHAVE)
	};
}
