using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Carência do contrato.
/// </summary>
[Table("T009CARE", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_CAR))]
public sealed class T009CARE
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_CAR", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal CD_CAR { get; set; }

	[Required]
	[MaxLength(30)]
	[Column("DE_CAR", TypeName = "varchar(30)")]
	public string DE_CAR { get; set; } = string.Empty;
}
