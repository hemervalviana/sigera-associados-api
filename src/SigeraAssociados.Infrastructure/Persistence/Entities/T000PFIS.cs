using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000PFIS: dados de pessoa física.
/// Chave e FK para T000PESS: CD_EMP + SQ_PES_FIS = CD_EMP + SQ_PES.
/// </summary>
[Table("T000PFIS", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_FIS))]
public sealed class T000PFIS
{
	/// <summary>
	/// Código da empresa. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Pessoa (T000PESS.SQ_PES). SQL Server: numeric(7,0), NOT NULL.
	/// </summary>
	[Column("SQ_PES_FIS", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_FIS { get; set; }

	/// <summary>
	/// Tratamento (T000TRTA). SQL Server: numeric(2,0), NULL.
	/// </summary>
	[Column("CD_TRT", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_TRT { get; set; }

	[MaxLength(40)]
	[Column("NR_RG", TypeName = "varchar(40)")]
	public string? NR_RG { get; set; }

	[MaxLength(10)]
	[Column("NM_ORG_EXP", TypeName = "varchar(10)")]
	public string? NM_ORG_EXP { get; set; }

	[MaxLength(8)]
	[Column("SG_UF_ORG", TypeName = "varchar(8)")]
	public string? SG_UF_ORG { get; set; }

	/// <summary>
	/// CPF. SQL Server: numeric(11,0), NULL.
	/// </summary>
	[Column("NR_CPF", TypeName = "numeric(11, 0)")]
	[Precision(11, 0)]
	public decimal? NR_CPF { get; set; }

	[Column("DT_NAS", TypeName = "smalldatetime")]
	public DateTime? DT_NAS { get; set; }

	/// <summary>
	/// Município (T000MUNI). SQL Server: numeric(7,0), NULL.
	/// </summary>
	[Column("CD_MUN", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? CD_MUN { get; set; }

	[MaxLength(1)]
	[Column("ID_SEX", TypeName = "varchar(1)")]
	public string? ID_SEX { get; set; }

	[MaxLength(1)]
	[Column("TP_EST_CIV", TypeName = "varchar(1)")]
	public string? TP_EST_CIV { get; set; }

	[MaxLength(50)]
	[Column("NM_CJG", TypeName = "varchar(50)")]
	public string? NM_CJG { get; set; }

	[Column("DT_NAS_CJG", TypeName = "smalldatetime")]
	public DateTime? DT_NAS_CJG { get; set; }

	[Column("DT_CAS", TypeName = "smalldatetime")]
	public DateTime? DT_CAS { get; set; }

	[MaxLength(50)]
	[Column("NM_PAI", TypeName = "varchar(50)")]
	public string? NM_PAI { get; set; }

	[MaxLength(50)]
	[Column("NM_MAE", TypeName = "varchar(50)")]
	public string? NM_MAE { get; set; }
}
