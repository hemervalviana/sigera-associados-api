using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Categoria do contrato (T009CTRT.CD_CAT; FK FK_T009CTRT_T009CAT criada por
/// scripts/contratos-fk-categoria.sql).
/// </summary>
[Table("T009CAT", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_CAT))]
public sealed class T009CAT
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	[Column("CD_CAT", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal CD_CAT { get; set; }

	[Required]
	[MaxLength(35)]
	[Column("NM_CAT", TypeName = "varchar(35)")]
	public string NM_CAT { get; set; } = string.Empty;
}
