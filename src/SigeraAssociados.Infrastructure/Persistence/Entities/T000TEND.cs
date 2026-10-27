using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000TEND: tipos de endereço
/// (residencial, comercial...). Referenciada por T000ENDE.CD_TP_EN.
/// </summary>
[Table("T000TEND", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_TP_EN))]
public sealed class T000TEND
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do tipo. NOT NULL.</summary>
	[Column("CD_TP_EN", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_TP_EN { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(20)]
	[Column("DE_TP_EN", TypeName = "varchar(20)")]
	public string DE_TP_EN { get; set; } = string.Empty;
}
