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
/// Categorias do contrato (T009CAT). A API confere o uso antes de excluir,
/// com ou sem a FK FK_T009CTRT_T009CAT (scripts/contratos-fk-categoria.sql).
/// </summary>
public sealed class CategoriasContratoService : ICategoriasContratoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;

	public CategoriasContratoService(SigeraDbContextFactory factory, SigeraSystemContext sistema)
	{
		_factory = factory;
		_sistema = sistema;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<CategoriaContratoDados>> ListarAsync(string? busca, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return [];
		}

		var query = db.Categorias.AsNoTracking().Where(c => c.CD_EMP == emp);

		if (!string.IsNullOrWhiteSpace(busca))
		{
			var texto = busca.Trim();

			query = decimal.TryParse(texto, out var codigo) && codigo % 1 == 0
				? query.Where(c => c.CD_CAT == codigo || c.NM_CAT.StartsWith(texto))
				: query.Where(c => c.NM_CAT.StartsWith(texto));
		}

		return (await query.OrderBy(c => c.NM_CAT).ThenBy(c => c.CD_CAT).Take(500).ToListAsync(cancellationToken))
			.Select(ParaDados)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CategoriaContratoDados>> IncluirAsync(CategoriaContratoDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoCategoriaContrato.Validar(dados, inclusao: true) is string erro)
		{
			return Invalido<CategoriaContratoDados>(erro);
		}

		await using var db = CriarContexto();

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<CategoriaContratoDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdCategoria ?? await NumeroAsync(db,
			$"SELECT ISNULL(MAX(CD_CAT), 0) + 1 AS [Value] FROM dbo.T009CAT WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}",
			cancellationToken);

		if (codigo > ValidacaoCategoriaContrato.CodigoMaximo)
		{
			return Invalido<CategoriaContratoDados>("Não há código de categoria livre (máximo 999).");
		}

		if (await db.Categorias.AnyAsync(c => c.CD_EMP == emp && c.CD_CAT == codigo, cancellationToken))
		{
			return Conflito<CategoriaContratoDados>($"A categoria {codigo:0} já existe.");
		}

		var categoria = new T009CAT { CD_EMP = emp, CD_CAT = codigo, NM_CAT = dados.Descricao!.Trim() };
		db.Categorias.Add(categoria);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<CategoriaContratoDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Categoria cadastrada.", ParaDados(categoria));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CategoriaContratoDados>> AlterarAsync(decimal cdCategoria, CategoriaContratoDados dados, CancellationToken cancellationToken = default)
	{
		if (ValidacaoCategoriaContrato.Validar(dados, inclusao: false) is string erro)
		{
			return Invalido<CategoriaContratoDados>(erro);
		}

		await using var db = CriarContexto();

		var categoria = await BuscarAsync(db, cdCategoria, cancellationToken);

		if (categoria is null)
		{
			return NaoEncontrado<CategoriaContratoDados>("Categoria não encontrada.");
		}

		categoria.NM_CAT = dados.Descricao!.Trim();

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Invalido<CategoriaContratoDados>(erroGravacao)
			: Sucesso("Categoria alterada.", ParaDados(categoria));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<CategoriaContratoDados>> ExcluirAsync(decimal cdCategoria, CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		var categoria = await BuscarAsync(db, cdCategoria, cancellationToken);

		if (categoria is null)
		{
			return NaoEncontrado<CategoriaContratoDados>("Categoria não encontrada.");
		}

		// Inclui contratos excluídos pela API: eles continuam no banco.
		if (await db.Contratos.AnyAsync(c => c.CD_EMP == categoria.CD_EMP && c.CD_CAT == cdCategoria, cancellationToken))
		{
			return Conflito<CategoriaContratoDados>($"A categoria {cdCategoria:0} não pode ser excluída: há contratos com ela.");
		}

		var dto = ParaDados(categoria);
		db.Categorias.Remove(categoria);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<CategoriaContratoDados>(erroGravacao)
			: Sucesso("Categoria excluída.", dto);
	}

	private static async Task<T009CAT?> BuscarAsync(SigeraDbContext db, decimal cdCategoria, CancellationToken cancellationToken) =>
		await ObterEmpresaAsync(db, cancellationToken) is decimal emp
			? await db.Categorias.FirstOrDefaultAsync(c => c.CD_EMP == emp && c.CD_CAT == cdCategoria, cancellationToken)
			: null;

	private static CategoriaContratoDados ParaDados(T009CAT c) => new()
	{
		CdCategoria = c.CD_CAT,
		Descricao = c.NM_CAT
	};

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}
}
