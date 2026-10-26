using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Infrastructure.Persistence.Factories;

namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Consulta o banco de negócio do sistema informado (somente leitura)
/// para validar os dados de uma concessão de acesso.
/// </summary>
public sealed class VerificadorCadastroSistema : IVerificadorCadastroSistema
{
	private readonly SigeraDbContextFactory _factory;

	public VerificadorCadastroSistema(SigeraDbContextFactory factory)
	{
		_factory = factory;
	}

	/// <inheritdoc />
	public async Task<decimal?> ObterEmpresaAsync(
		int sistema,
		CancellationToken cancellationToken = default)
	{
		await using var db = _factory.Create(sistema);

		// Lê no máximo duas: basta saber se existe exatamente uma.
		var empresas = await db.Empresas
			.AsNoTracking()
			.Select(e => e.CD_EMP)
			.Take(2)
			.ToListAsync(cancellationToken);

		return empresas.Count == 1 ? empresas[0] : null;
	}

	/// <inheritdoc />
	public async Task<bool> FilialExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal cdFil,
		CancellationToken cancellationToken = default)
	{
		await using var db = _factory.Create(sistema);

		return await db.Filiais
			.AsNoTracking()
			.AnyAsync(f => f.CD_EMP == cdEmp && f.CD_FIL == cdFil, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> CorretorExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal sqPesCorretor,
		decimal? cdFil,
		CancellationToken cancellationToken = default)
	{
		await using var db = _factory.Create(sistema);

		var corretores = db.Corretores
			.AsNoTracking()
			.Where(c => c.CD_EMP == cdEmp && c.SQ_PES_CRO == sqPesCorretor);

		if (cdFil is decimal filial)
		{
			corretores = corretores.Where(c => c.CD_FIL == filial);
		}

		return await corretores.AnyAsync(cancellationToken);
	}
}
