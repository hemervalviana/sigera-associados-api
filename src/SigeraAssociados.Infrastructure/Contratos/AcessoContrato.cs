using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Repositories;

namespace SigeraAssociados.Infrastructure.Contratos;

/// <summary>
/// Quais contratos o usuário alcança: não excluídos, de associado não
/// excluído, dentro do escopo (filial e, para a carteira, o corretor do
/// próprio contrato).
/// </summary>
internal static class AcessoContrato
{
	public static IQueryable<T009CTRT> Validos(SigeraDbContext db, decimal emp) =>
		db.Contratos.Where(c =>
			c.CD_EMP == emp
			&& c.DT_EXC_API == null
			&& db.Associados.Any(a => a.CD_EMP == c.CD_EMP && a.SQ_PES_CLI == c.SQ_PES_CLI && a.DT_EXC_API == null));

	public static IQueryable<T009CTRT> AplicarEscopo(IQueryable<T009CTRT> query, EscopoConsulta escopo)
	{
		if (escopo.AcessoTotal)
		{
			return query;
		}

		Expression<Func<T009CTRT, bool>>? filtro = null;

		foreach (var item in escopo.Escopos.Distinct())
		{
			var filial = item.CdFil;
			var corretor = item.SqPesCorretor;

			Expression<Func<T009CTRT, bool>> condicao = (filial, corretor) switch
			{
				(null, null) => c => true,
				(decimal f, null) => c => c.CD_FIL == f,
				(null, decimal r) => c => c.SQ_PES_CRO == r,
				(decimal f, decimal r) => c => c.CD_FIL == f && c.SQ_PES_CRO == r
			};

			filtro = filtro is null ? condicao : AssociadosRepository.Ou(filtro, condicao);
		}

		return filtro is null ? query.Where(_ => false) : query.Where(filtro);
	}

	public static bool Permite(EscopoConsulta escopo, decimal cdFil, decimal corretor) =>
		!escopo.SemAcesso && EscopoEscrita.PermiteAssociado(escopo, cdFil, [corretor]);

	/// <summary>Contrato válido e no escopo, ou null.</summary>
	public static async Task<T009CTRT?> CarregarAsync(
		SigeraDbContext db,
		decimal emp,
		decimal cdFil,
		decimal sqCtr,
		EscopoConsulta escopo,
		bool rastrear,
		CancellationToken cancellationToken)
	{
		if (escopo.SemAcesso)
		{
			return null;
		}

		var query = Validos(db, emp).Where(c => c.CD_FIL == cdFil && c.SQ_CTR == sqCtr);

		var contrato = await (rastrear ? query : query.AsNoTracking()).FirstOrDefaultAsync(cancellationToken);

		return contrato is not null && Permite(escopo, contrato.CD_FIL, contrato.SQ_PES_CRO) ? contrato : null;
	}

	/// <summary>
	/// Acerta QT_PCL (quantidade de parcelas) e, no produto 2, DT_ULT_DCT
	/// (vencimento da última parcela), como o pfc_preupdate do legado.
	/// </summary>
	public static async Task SincronizarParcelasAsync(SigeraDbContext db, T009CTRT contrato, CancellationToken cancellationToken)
	{
		var parcelas = db.Parcelas.Where(p => p.CD_EMP == contrato.CD_EMP && p.CD_FIL == contrato.CD_FIL && p.SQ_CTR == contrato.SQ_CTR);

		contrato.QT_PCL = await parcelas.CountAsync(cancellationToken);

		if (contrato.CD_PRD == Application.Contratos.RegrasContrato.ProdutoAuxilioFinanceiro)
		{
			contrato.DT_ULT_DCT = await parcelas.MaxAsync(p => (DateTime?)p.DT_VEN, cancellationToken);
		}
	}
}
