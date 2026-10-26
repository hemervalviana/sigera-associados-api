using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000MUNI: municípios.
/// Referenciada por T000PFIS.CD_MUN. Somente leitura.
/// </summary>
[Table("T000MUNI", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_MUN))]
public sealed class T000MUNI
{
	/// <summary>
	/// Código da empresa. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código do município. SQL Server: numeric(7,0), NOT NULL.
	/// </summary>
	[Column("CD_MUN", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal CD_MUN { get; set; }

	/// <summary>
	/// Nome do município. SQL Server: varchar(400), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(400)]
	[Column("DE_MUN", TypeName = "varchar(400)")]
	public string DE_MUN { get; set; } = string.Empty;

	/// <summary>
	/// UF. SQL Server: varchar(2), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(2)]
	[Column("SG_UF", TypeName = "varchar(2)")]
	public string SG_UF { get; set; } = string.Empty;
}
