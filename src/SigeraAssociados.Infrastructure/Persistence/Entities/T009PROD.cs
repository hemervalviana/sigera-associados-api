using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Produtor do corretor (T009CTRT.SQ_PRD).
/// </summary>
[Table("T009PROD", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PRD))]
public sealed class T009PROD
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("SQ_PRD", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PRD { get; set; }

	[Column("SQ_PES_CRO", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CRO { get; set; }

	[Required]
	[MaxLength(80)]
	[Column("NM_PRD", TypeName = "varchar(80)")]
	public string NM_PRD { get; set; } = string.Empty;
}
