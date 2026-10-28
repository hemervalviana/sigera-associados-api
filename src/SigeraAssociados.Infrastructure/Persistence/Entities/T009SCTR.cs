using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Situação do contrato.
/// </summary>
[Table("T009SCTR", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_ST_CTR))]
public sealed class T009SCTR
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_ST_CTR", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_ST_CTR { get; set; }

	[Required]
	[MaxLength(50)]
	[Column("DE_ST_CTR", TypeName = "varchar(50)")]
	public string DE_ST_CTR { get; set; } = string.Empty;
}
