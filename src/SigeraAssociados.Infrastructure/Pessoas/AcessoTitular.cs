using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Associados;
using SigeraAssociados.Infrastructure.Persistence.Context;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Pessoas;

/// <summary>
/// Conferência de acesso aos dados por pessoa (endereços, telefones,
/// contas, referências e Pix) conforme o titular.
/// </summary>
internal static class AcessoTitular
{
	/// <summary>
	/// Empresa da base quando o titular existe e pode ser usado; null para
	/// tratar como não encontrado.
	/// - Associado: não excluído, pessoa física, dentro do escopo do usuário.
	/// - Corretor: não excluído e coerente (F com T000PFIS e sem T000PJUR; J o contrário).
	/// </summary>
	public static async Task<decimal?> AbrirAsync(
		SigeraDbContext db,
		IEscopoAcessoService escopoAcesso,
		TitularCadastro titular,
		decimal sqPes,
		CancellationToken cancellationToken)
	{
		var escopo = await escopoAcesso.ObterEscopoAsync(cancellationToken);

		if (escopo.SemAcesso || await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return null;
		}

		var permitido = titular == TitularCadastro.Corretor
			? await CorretorValidoAsync(db, emp, sqPes, cancellationToken)
			: await AcessoAssociado.PermitidoAsync(db, emp, sqPes, escopo, cancellationToken);

		return permitido ? emp : null;
	}

	public static string NaoEncontrado(TitularCadastro titular) =>
		titular == TitularCadastro.Corretor ? "Corretor não encontrado." : "Associado não encontrado.";

	private static async Task<bool> CorretorValidoAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPes,
		CancellationToken cancellationToken)
	{
		var tipo = await (
				from c in db.Corretores.AsNoTracking()
				where c.CD_EMP == emp && c.SQ_PES_CRO == sqPes && c.DT_EXC_API == null
				join p in db.Pessoas.AsNoTracking()
					on new { Empresa = c.CD_EMP, Pessoa = c.SQ_PES_CRO }
					equals new { Empresa = p.CD_EMP, Pessoa = p.SQ_PES }
				select p.TP_PES)
			.FirstOrDefaultAsync(cancellationToken);

		if (tipo is null)
		{
			return false;
		}

		var temFisica = await db.PessoasFisicas.AnyAsync(f => f.CD_EMP == emp && f.SQ_PES_FIS == sqPes, cancellationToken);
		var temJuridica = await db.PessoasJuridicas.AnyAsync(j => j.CD_EMP == emp && j.SQ_PES_JUR == sqPes, cancellationToken);

		return tipo switch
		{
			"F" => temFisica && !temJuridica,
			"J" => temJuridica && !temFisica,
			_ => false
		};
	}
}
