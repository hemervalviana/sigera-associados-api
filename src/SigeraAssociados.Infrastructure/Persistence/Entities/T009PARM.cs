using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Parâmetros do sistema por filial (f_retornar_parametro do legado).
/// Parâmetro ligado: TP_FLG = 'S'.
/// </summary>
[Table("T009PARM", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_FIL), nameof(SQ_PRM))]
public sealed class T009PARM
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_FIL { get; set; }

	[Column("SQ_PRM", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal SQ_PRM { get; set; }

	[MaxLength(80)]
	[Column("DE_PRM", TypeName = "varchar(80)")]
	public string? DE_PRM { get; set; }

	[MaxLength(1)]
	[Column("TP_FLG", TypeName = "varchar(1)")]
	public string? TP_FLG { get; set; }
}
