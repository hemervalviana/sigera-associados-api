using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000TRTA: tratamentos
/// (Sr., Sra., Dr. ...). Referenciada por T000PFIS.CD_TRT. Somente leitura.
/// </summary>
[Table("T000TRTA", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_TRT))]
public sealed class T000TRTA
{
	/// <summary>
	/// Código da empresa. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código do tratamento. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_TRT", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_TRT { get; set; }

	/// <summary>
	/// Descrição. SQL Server: varchar(20), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(20)]
	[Column("DE_TRT", TypeName = "varchar(20)")]
	public string DE_TRT { get; set; } = string.Empty;
}
