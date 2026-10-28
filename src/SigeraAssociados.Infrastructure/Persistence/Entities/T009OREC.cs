using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Origem do recurso do contrato.
/// </summary>
[Table("T009OREC", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_ORI_REC))]
public sealed class T009OREC
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_ORI_REC", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal CD_ORI_REC { get; set; }

	[Required]
	[MaxLength(50)]
	[Column("DE_ORI_REC", TypeName = "varchar(50)")]
	public string DE_ORI_REC { get; set; } = string.Empty;

	[Required]
	[MaxLength(1)]
	[Column("TP_ORI_REC", TypeName = "varchar(1)")]
	public string TP_ORI_REC { get; set; } = string.Empty;
}
