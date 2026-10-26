using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Context;

namespace SigeraAssociados.Infrastructure.Associados;

/// <summary>
/// Confere se o usuário alcança um associado: existe, não foi excluído
/// pela API e está dentro do escopo (filial e carteira do corretor:
/// contratos ou cadastro feito pelo corretor).
/// </summary>
internal static class AcessoAssociado
{
	public static async Task<bool> PermitidoAsync(
		SigeraDbContext db,
		decimal empresa,
		decimal sqPesCli,
		EscopoConsulta escopo,
		CancellationToken cancellationToken)
	{
		if (escopo.SemAcesso)
		{
			return false;
		}

		var associado = await db.Associados
			.AsNoTracking()
			.Where(a => a.CD_EMP == empresa && a.SQ_PES_CLI == sqPesCli && a.DT_EXC_API == null)
			.Select(a => new { a.CD_FIL, a.SQ_PES_CRO_CAD })
			.FirstOrDefaultAsync(cancellationToken);

		if (associado is null)
		{
			return false;
		}

		if (escopo.AcessoTotal)
		{
			return true;
		}

		var corretores = await db.Contratos
			.AsNoTracking()
			.Where(c => c.CD_EMP == empresa && c.SQ_PES_CLI == sqPesCli)
			.Select(c => c.SQ_PES_CRO)
			.Distinct()
			.ToListAsync(cancellationToken);

		if (associado.SQ_PES_CRO_CAD is decimal cadastradoPor)
		{
			corretores.Add(cadastradoPor);
		}

		return EscopoEscrita.PermiteAssociado(escopo, associado.CD_FIL, corretores);
	}
}
