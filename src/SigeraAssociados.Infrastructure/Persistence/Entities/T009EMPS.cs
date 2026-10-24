using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009EMPS.
/// Contém o código e o nome da empresa.
/// </summary>
[Table("T009EMPS", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP))]
public sealed class T009EMPS
{
	/// <summary>
	/// Código da empresa.
	/// SQL Server: numeric(2,0), NOT NULL.
	/// Chave primária.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Nome da empresa.
	/// SQL Server: varchar(80), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(80)]
	[Column("NM_EMP", TypeName = "varchar(80)")]
	public string NM_EMP { get; set; } = string.Empty;
}