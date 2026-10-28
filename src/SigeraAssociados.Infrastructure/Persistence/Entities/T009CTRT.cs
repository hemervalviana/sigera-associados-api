using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Contrato do associado. Chave: empresa, filial e SQ_CTR (MAX + 1 da filial).
/// QT_PCL acompanha a quantidade de parcelas em T009PAGA.
/// </summary>
[Table("T009CTRT", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_FIL), nameof(SQ_CTR))]
public sealed class T009CTRT
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_FIL { get; set; }

	[Column("SQ_CTR", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal SQ_CTR { get; set; }

	[Column("SQ_PES_CLI", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CLI { get; set; }

	[Column("SQ_PES_CRO", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CRO { get; set; }

	[Column("CD_MUN", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? CD_MUN { get; set; }

	[Column("TP_FMA_PGT", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal TP_FMA_PGT { get; set; }

	[Column("DT_PRI_PCL", TypeName = "datetime")]
	public DateTime? DT_PRI_PCL { get; set; }

	[Column("QT_PCL", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal? QT_PCL { get; set; }

	[Column("VR_CTR", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_CTR { get; set; }

	[Column("DT_PGT_AUX", TypeName = "datetime")]
	public DateTime? DT_PGT_AUX { get; set; }

	[Column("DT_CTR", TypeName = "datetime")]
	public DateTime DT_CTR { get; set; }

	[Column("DT_ENC_CTR", TypeName = "datetime")]
	public DateTime? DT_ENC_CTR { get; set; }

	[Column("VR_TAR_BCO", TypeName = "numeric(4, 2)")]
	[Precision(4, 2)]
	public decimal? VR_TAR_BCO { get; set; }

	[Column("DT_PGT_CMS", TypeName = "datetime")]
	public DateTime? DT_PGT_CMS { get; set; }

	[MaxLength(2000)]
	[Column("OB_CTR", TypeName = "varchar(2000)")]
	public string? OB_CTR { get; set; }

	[Column("DT_CAD", TypeName = "datetime")]
	public DateTime DT_CAD { get; set; }

	[Column("VR_DSP_ADM", TypeName = "numeric(7, 2)")]
	[Precision(7, 2)]
	public decimal? VR_DSP_ADM { get; set; }

	[Column("VR_MNS", TypeName = "numeric(7, 2)")]
	[Precision(7, 2)]
	public decimal? VR_MNS { get; set; }

	[Column("VR_INA", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_INA { get; set; }

	[Column("VR_CMS", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_CMS { get; set; }

	[Column("CD_CAR", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_CAR { get; set; }

	[Column("CD_ST_CTR", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_ST_CTR { get; set; }

	[Column("SQ_PES_CCG", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? SQ_PES_CCG { get; set; }

	[Column("SQ_CTA_COR", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? SQ_CTA_COR { get; set; }

	[Column("CD_ORG", TypeName = "numeric(6, 0)")]
	[Precision(6, 0)]
	public decimal? CD_ORG { get; set; }

	[Column("CD_CVN", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_CVN { get; set; }

	[Column("VR_IND", TypeName = "numeric(5, 4)")]
	[Precision(5, 4)]
	public decimal? VR_IND { get; set; }

	[Column("CD_PRD", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_PRD { get; set; }

	[MaxLength(20)]
	[Column("NM_USU", TypeName = "varchar(20)")]
	public string? NM_USU { get; set; }

	[Column("NR_PRP", TypeName = "numeric(6, 0)")]
	[Precision(6, 0)]
	public decimal? NR_PRP { get; set; }

	[Column("DT_ULT_DCT", TypeName = "datetime")]
	public DateTime? DT_ULT_DCT { get; set; }

	[MaxLength(255)]
	[Column("OB_SNT", TypeName = "varchar(255)")]
	public string? OB_SNT { get; set; }

	[Column("CD_ORI_REC", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? CD_ORI_REC { get; set; }

	[Column("SQ_ORG", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? SQ_ORG { get; set; }

	[Column("VR_CBT_PEC", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_CBT_PEC { get; set; }

	[Column("SQ_PRD", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? SQ_PRD { get; set; }

	[Column("VR_DEP", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_DEP { get; set; }

	[Column("CD_BCO", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_BCO { get; set; }

	[Column("VR_PGT_FORA", TypeName = "decimal(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_PGT_FORA { get; set; }

	[Column("CD_CAT", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_CAT { get; set; }

	[Column("CD_TP_CTRT", TypeName = "decimal(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_TP_CTRT { get; set; }

	[Column("DT_QIT_CTRT", TypeName = "datetime")]
	public DateTime? DT_QIT_CTRT { get; set; }

	[Column("DT_CAN_CTRT", TypeName = "datetime")]
	public DateTime? DT_CAN_CTRT { get; set; }

	[Column("CD_ENT_FISI", TypeName = "decimal(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_ENT_FISI { get; set; }

	[Column("VR_QIT_CTR", TypeName = "decimal(13, 2)")]
	[Precision(13, 2)]
	public decimal? VR_QIT_CTR { get; set; }

	[Column("DT_FIN_CTR", TypeName = "datetime")]
	public DateTime? DT_FIN_CTR { get; set; }

	[Column("VR_CMP_DIV", TypeName = "decimal(13, 2)")]
	[Precision(13, 2)]
	public decimal? VR_CMP_DIV { get; set; }

	[Column("SQ_CTA_COR_CRRT", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? SQ_CTA_COR_CRRT { get; set; }

	[Column("VR_CMP_DIV_TP", TypeName = "decimal(2, 0)")]
	[Precision(2, 0)]
	public decimal? VR_CMP_DIV_TP { get; set; }

	[Column("DT_PRV_QIT", TypeName = "date")]
	public DateTime? DT_PRV_QIT { get; set; }

	/// <summary>Data da exclusão lógica feita pela API (coluna criada por scripts/contratos-colunas-api-e-permissoes.sql).</summary>
	[Column("DT_EXC_API", TypeName = "datetime")]
	public DateTime? DT_EXC_API { get; set; }

	/// <summary>Quem fez a exclusão lógica (e-mail). Coluna criada pelo mesmo script.</summary>
	[MaxLength(256)]
	[Column("NM_USU_EXC_API", TypeName = "varchar(256)")]
	public string? NM_USU_EXC_API { get; set; }
}
