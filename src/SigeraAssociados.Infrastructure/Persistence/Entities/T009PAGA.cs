using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Parcela do contrato (FK para T009CTRT com ON DELETE CASCADE).
/// NR_PCL = MAX + 1 do contrato. VR_SD_PCL nasce com -VR_PCL, como no legado.
/// </summary>
[Table("T009PAGA", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_FIL), nameof(SQ_CTR), nameof(NR_PCL))]
public sealed class T009PAGA
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

	[Column("NR_PCL", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal NR_PCL { get; set; }

	[Column("DT_VEN", TypeName = "datetime")]
	public DateTime DT_VEN { get; set; }

	[Column("DT_PGT", TypeName = "datetime")]
	public DateTime? DT_PGT { get; set; }

	[Column("VR_PCL", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal VR_PCL { get; set; }

	[Column("VR_PGT", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_PGT { get; set; }

	[Column("ID_REM", TypeName = "bit")]
	public bool? ID_REM { get; set; }

	[Column("NR_CHQ", TypeName = "numeric(10, 0)")]
	[Precision(10, 0)]
	public decimal? NR_CHQ { get; set; }

	[Column("VR_MLT", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_MLT { get; set; }

	[Column("VR_JUR", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_JUR { get; set; }

	[Column("VR_DSC", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_DSC { get; set; }

	[Column("DT_GER_PCL", TypeName = "datetime")]
	public DateTime? DT_GER_PCL { get; set; }

	[Column("VR_SD_PCL", TypeName = "numeric(13, 2)")]
	[Precision(13, 2)]
	public decimal? VR_SD_PCL { get; set; }

	[MaxLength(50)]
	[Column("NM_USU", TypeName = "varchar(50)")]
	public string? NM_USU { get; set; }

	[Column("ID_PGT_AUT", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? ID_PGT_AUT { get; set; }

	[Column("TP_PGT", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? TP_PGT { get; set; }

	[Column("CD_CVN", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_CVN { get; set; }

	[Column("CD_AVB", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? CD_AVB { get; set; }

	[MaxLength(1)]
	[Column("ID_COB_PCL", TypeName = "varchar(1)")]
	public string? ID_COB_PCL { get; set; }
}
