using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Índice da mensalidade por carência e quantidade de parcelas
/// (mensalidade = valor do contrato x índice).
/// </summary>
[Table("T009INDI", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_CAR), nameof(QT_PCL))]
public sealed class T009INDI
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_CAR", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal CD_CAR { get; set; }

	[Column("QT_PCL", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal QT_PCL { get; set; }

	[Column("VR_IND", TypeName = "numeric(5, 4)")]
	[Precision(5, 4)]
	public decimal VR_IND { get; set; }
}
