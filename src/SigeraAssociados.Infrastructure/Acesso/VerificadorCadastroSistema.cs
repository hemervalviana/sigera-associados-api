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
	public async Task<bool> EmpresaExisteAsync(
		int sistema,
		decimal cdEmp,
		CancellationToken cancellationToken = default)
	{
		await using var db = _factory.Create(sistema);

		return await db.Empresas
			.AsNoTracking()
			.AnyAsync(e => e.CD_EMP == cdEmp, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> FilialExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal cdFil,
		CancellationToken cancellationToken = default)
	{
		await using var db = _factory.Create(sistema);

		return await db.Associados
				.AsNoTracking()
				.AnyAsync(a => a.CD_EMP == cdEmp && a.CD_FIL == cdFil, cancellationToken)
			|| await db.Contratos
				.AsNoTracking()
				.AnyAsync(c => c.CD_EMP == cdEmp && c.CD_FIL == cdFil, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> CorretorExisteAsync(
		int sistema,
		decimal cdEmp,
		decimal sqPesCorretor,
		CancellationToken cancellationToken = default)
	{
		await using var db = _factory.Create(sistema);

		var pessoaExiste = await db.Pessoas
			.AsNoTracking()
			.AnyAsync(p => p.CD_EMP == cdEmp && p.SQ_PES == sqPesCorretor, cancellationToken);

		return pessoaExiste
			&& await db.Contratos
				.AsNoTracking()
				.AnyAsync(c => c.CD_EMP == cdEmp && c.SQ_PES_CRO == sqPesCorretor, cancellationToken);
	}
}
