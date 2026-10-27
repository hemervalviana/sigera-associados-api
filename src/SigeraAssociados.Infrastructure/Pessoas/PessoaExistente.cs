using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Corretores;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Pessoas;

/// <summary>
/// Apoio para aproveitar uma pessoa já cadastrada num papel novo
/// (associado ↔ corretor): lê o que ela já tem e completa só os campos
/// vazios com os dados enviados. O que já está gravado não muda.
/// </summary>
internal static class PessoaExistente
{
	public static async Task<RegrasPessoaExistente.Situacao> CarregarAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPes,
		CancellationToken cancellationToken)
	{
		var tipo = await db.Pessoas.AsNoTracking()
			.Where(p => p.CD_EMP == emp && p.SQ_PES == sqPes)
			.Select(p => p.TP_PES)
			.FirstOrDefaultAsync(cancellationToken);

		var associado = await db.Associados.AsNoTracking()
			.Where(a => a.CD_EMP == emp && a.SQ_PES_CLI == sqPes)
			.Select(a => new { Excluido = a.DT_EXC_API != null })
			.FirstOrDefaultAsync(cancellationToken);

		var corretor = await db.Corretores.AsNoTracking()
			.Where(c => c.CD_EMP == emp && c.SQ_PES_CRO == sqPes)
			.Select(c => new { Excluido = c.DT_EXC_API != null })
			.FirstOrDefaultAsync(cancellationToken);

		return new RegrasPessoaExistente.Situacao(
			sqPes,
			tipo,
			await db.PessoasFisicas.AnyAsync(f => f.CD_EMP == emp && f.SQ_PES_FIS == sqPes, cancellationToken),
			await db.PessoasJuridicas.AnyAsync(j => j.CD_EMP == emp && j.SQ_PES_JUR == sqPes, cancellationToken),
			associado is not null,
			associado?.Excluido == true,
			corretor is not null,
			corretor?.Excluido == true);
	}

	/// <summary>Completa os campos vazios de T000PESS (nome nunca muda).</summary>
	public static void ComplementarPessoa(T000PESS p, string? nomeCurto, string? email, string? url, string? observacao)
	{
		p.NC_PES ??= Texto(nomeCurto);
		p.DE_COR_ELE ??= Texto(email);
		p.DE_URL ??= Texto(url);
		p.OB_PES ??= Texto(observacao);
	}

	/// <summary>Completa os campos vazios de T000PFIS (o CPF é o mesmo).</summary>
	public static void ComplementarFisica(T000PFIS f, PessoaFisicaDados pf)
	{
		f.NR_RG ??= Texto(pf.Rg);
		f.NM_ORG_EXP ??= Texto(pf.OrgaoExpedidor);
		f.SG_UF_ORG ??= Texto(pf.UfOrgao)?.ToUpperInvariant();
		f.DT_NAS ??= pf.DataNascimento?.Date;
		f.ID_SEX ??= Texto(pf.Sexo)?.ToUpperInvariant();
		f.TP_EST_CIV ??= Texto(pf.EstadoCivil)?.ToUpperInvariant();
		f.CD_MUN ??= pf.CdMunicipio;
		f.CD_TRT ??= pf.CdTratamento;
		f.NM_PAI ??= Texto(pf.NomePai);
		f.NM_MAE ??= Texto(pf.NomeMae);
		f.NM_CJG ??= Texto(pf.NomeConjuge);
		f.DT_NAS_CJG ??= pf.DataNascimentoConjuge?.Date;
		f.DT_CAS ??= pf.DataCasamento?.Date;
	}

	/// <summary>Completa os campos vazios de T000PJUR (o CNPJ é o mesmo).</summary>
	public static void ComplementarJuridica(T000PJUR j, PessoaJuridicaDados pj)
	{
		j.NR_CGF ??= pj.InscricaoEstadual;
		j.NM_CON ??= Texto(pj.Contato);
		j.CD_CG ??= pj.CdCargo;
	}
}
