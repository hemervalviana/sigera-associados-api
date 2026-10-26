using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000LOTA: lotações.
/// Referenciada por T000RPOR.CD_LOT.
/// </summary>
[Table("T000LOTA", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_LOT))]
public sealed class T000LOTA
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código da lotação. NOT NULL.</summary>
	[Column("CD_LOT", TypeName = "decimal(6, 0)")]
	[Precision(6, 0)]
	public decimal CD_LOT { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(50)]
	[Column("DE_LOT", TypeName = "varchar(50)")]
	public string DE_LOT { get; set; } = string.Empty;

	/// <summary>Situação da lotação (0 a 9).</summary>
	[Column("CD_SIT_LOT", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_SIT_LOT { get; set; }
}
