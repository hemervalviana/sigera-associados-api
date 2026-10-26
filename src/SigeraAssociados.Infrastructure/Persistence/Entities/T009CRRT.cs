using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009CRRT: cadastro de corretores.
/// O nome do corretor fica em T000PESS (CD_EMP + SQ_PES_CRO = CD_EMP + SQ_PES).
/// Somente leitura.
/// </summary>
[Table("T009CRRT", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_CRO))]
public sealed class T009CRRT
{
	/// <summary>
	/// Código da empresa. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Pessoa corretor (T000PESS.SQ_PES). SQL Server: numeric(7,0), NOT NULL.
	/// </summary>
	[Column("SQ_PES_CRO", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CRO { get; set; }

	/// <summary>
	/// Corretor representante (T009CRRT.SQ_PES_CRO). numeric(7,0), NULL.
	/// </summary>
	[Column("SQ_PES_RPR", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? SQ_PES_RPR { get; set; }

	[Column("NR_ISC_SUS", TypeName = "numeric(15, 0)")]
	[Precision(15, 0)]
	public decimal? NR_ISC_SUS { get; set; }

	[Column("PC_CMS_ADT", TypeName = "numeric(5, 2)")]
	[Precision(5, 2)]
	public decimal? PC_CMS_ADT { get; set; }

	[Column("PC_CMS_CAR", TypeName = "numeric(5, 2)")]
	[Precision(5, 2)]
	public decimal? PC_CMS_CAR { get; set; }

	[MaxLength(1)]
	[Column("CL_CRO", TypeName = "varchar(1)")]
	public string? CL_CRO { get; set; }

	[Column("CD_MUN", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? CD_MUN { get; set; }

	[Column("PC_CMS_PLN", TypeName = "numeric(5, 2)")]
	[Precision(5, 2)]
	public decimal? PC_CMS_PLN { get; set; }

	[MaxLength(20)]
	[Column("SH_RPR", TypeName = "varchar(20)")]
	public string? SH_RPR { get; set; }

	[Column("SQ_CDR", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? SQ_CDR { get; set; }

	/// <summary>
	/// Filial do corretor. SQL Server: numeric(2,0), NULL.
	/// </summary>
	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_FIL { get; set; }

	[Column("SQ_AGEN", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? SQ_AGEN { get; set; }

	[Column("SQ_GVED", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? SQ_GVED { get; set; }

	/// <summary>
	/// Situação do corretor. SQL Server: numeric(1,0), NULL.
	/// </summary>
	[Column("CD_SIT_CRRT", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_SIT_CRRT { get; set; }

	[Column("TP_CRRT", TypeName = "int")]
	public int? TP_CRRT { get; set; }
}
