using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009PRDT: produtos.
/// Referenciada (sem FK) por T009EXTRA.CD_PRD.
/// </summary>
[Table("T009PRDT", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_PRD))]
public sealed class T009PRDT
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do produto. NOT NULL.</summary>
	[Column("CD_PRD", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_PRD { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(100)]
	[Column("DE_PRD", TypeName = "varchar(100)")]
	public string DE_PRD { get; set; } = string.Empty;
}
